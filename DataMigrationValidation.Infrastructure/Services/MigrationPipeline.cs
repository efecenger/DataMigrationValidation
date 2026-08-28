using System.Text.Json;
using DataMigrationValidation.Core.Deduplication;
using DataMigrationValidation.Core.Entities;
using DataMigrationValidation.Core.Enums;
using DataMigrationValidation.Core.Reports;
using DataMigrationValidation.Core.Validation;
using DataMigrationValidation.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataMigrationValidation.Infrastructure.Services;

public sealed class MigrationPipeline
{
    private readonly LegacyDbContext _legacyContext;
    private readonly TargetDbContext _targetContext;
    private readonly Func<TargetDbContext> _targetContextFactory;
    private readonly LegacyCustomerValidator _customerValidator;
    private readonly LegacyOrderValidator _orderValidator;
    private readonly CustomerDeduplicator _customerDeduplicator;
    private readonly ReconciliationService _reconciliationService;
    private readonly MigrationExecutionOptions _options;
    private readonly ParallelMigrationWorkerPool _workerPool;

    public MigrationPipeline(
        LegacyDbContext legacyContext,
        TargetDbContext targetContext,
        Func<TargetDbContext> targetContextFactory,
        LegacyCustomerValidator customerValidator,
        LegacyOrderValidator orderValidator,
        CustomerDeduplicator customerDeduplicator,
        ReconciliationService reconciliationService,
        MigrationExecutionOptions options)
    {
        _legacyContext = legacyContext;
        _targetContext = targetContext;
        _targetContextFactory = targetContextFactory;
        _customerValidator = customerValidator;
        _orderValidator = orderValidator;
        _customerDeduplicator = customerDeduplicator;
        _reconciliationService = reconciliationService;
        _options = options;
        _workerPool = new ParallelMigrationWorkerPool(
            options.MaxDegreeOfParallelism);
    }

    public async Task<ReconciliationReport> RunAsync(
        bool simulateFailure = false,
        CancellationToken cancellationToken = default)
    {
        int sourceCustomerCount =
            await _legacyContext.LegacyCustomers
                .CountAsync(cancellationToken);

        int sourceOrderCount =
            await _legacyContext.LegacyOrders
                .CountAsync(cancellationToken);

        MigrationRun migrationRun = new()
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow,
            Status = MigrationStatus.Running,
            SourceCustomerCount = sourceCustomerCount,
            SourceOrderCount = sourceOrderCount
        };

        _targetContext.MigrationRuns.Add(migrationRun);

        await _targetContext.SaveChangesAsync(
            cancellationToken);

        int failedCustomerCount = 0;
        int failedOrderCount = 0;

        IDbContextTransaction? transaction = null;

        try
        {
            CustomerDeduplicationAccumulator accumulator =
                _customerDeduplicator.CreateAccumulator();

            long? lastCustomerId = null;

            while (true)
            {
                List<LegacyCustomer> customerBatch =
                    await LoadCustomerBatchAsync(
                        lastCustomerId,
                        cancellationToken);

                if (customerBatch.Count == 0)
                {
                    break;
                }

                IReadOnlyList<ValidationResult<LegacyCustomer>>
                    validationResults =
                        _workerPool.Process(
                            customerBatch,
                            _customerValidator.ValidateAndClean,
                            cancellationToken);

                List<FailedRecord> failureBatch = new();

                for (int index = 0;
                     index < customerBatch.Count;
                     index++)
                {
                    LegacyCustomer customer =
                        customerBatch[index];

                    ValidationResult<LegacyCustomer> result =
                        validationResults[index];

                    if (!result.IsValid)
                    {
                        failedCustomerCount++;

                        failureBatch.Add(
                            CreateValidationFailure(
                                migrationRun.Id,
                                "LegacyCustomers",
                                customer.LegacyId.ToString(),
                                customer,
                                result.Issues));

                        continue;
                    }

                    accumulator.Add(result.CleanedRecord);
                }

                await PersistFailuresAsync(
                    failureBatch,
                    cancellationToken);

                lastCustomerId =
                    customerBatch[^1].LegacyId;
            }

            DeduplicationResult deduplicationResult =
                accumulator.Complete();

            transaction =
                await _targetContext.Database
                    .BeginTransactionAsync(
                        cancellationToken);

            await _targetContext.Orders.ExecuteDeleteAsync(
                cancellationToken);

            await _targetContext.Customers.ExecuteDeleteAsync(
                cancellationToken);

            Dictionary<long, Guid> customerIdMap = new();

            foreach (DeduplicatedCustomer[] customerBatch in
                     deduplicationResult.Customers.Chunk(
                         _options.BatchSize))
            {
                List<Customer> targetCustomerBatch =
                    new(customerBatch.Length);

                foreach (DeduplicatedCustomer deduplicatedCustomer
                         in customerBatch)
                {
                    LegacyCustomer sourceCustomer =
                        deduplicatedCustomer.CanonicalRecord;

                    Guid newCustomerId = Guid.NewGuid();

                    targetCustomerBatch.Add(
                        new Customer
                        {
                            Id = newCustomerId,

                            NationalIdentityNumber =
                                sourceCustomer
                                    .NationalIdentityNumber,

                            FullName = sourceCustomer.FullName!,
                            Email = sourceCustomer.Email!,
                            Phone = sourceCustomer.Phone!,

                            CreatedAtUtc =
                                sourceCustomer.CreatedAt ??
                                DateTime.UtcNow
                        });

                    foreach (long legacyId in
                             deduplicatedCustomer.SourceLegacyIds)
                    {
                        customerIdMap[legacyId] =
                            newCustomerId;
                    }
                }

                _targetContext.Customers.AddRange(
                    targetCustomerBatch);

                await _targetContext.SaveChangesAsync(
                    cancellationToken);

                DetachRange(targetCustomerBatch);
            }

            int migratedOrderCount = 0;
            long? lastOrderId = null;

            while (true)
            {
                List<LegacyOrder> orderBatch =
                    await LoadOrderBatchAsync(
                        lastOrderId,
                        cancellationToken);

                if (orderBatch.Count == 0)
                {
                    break;
                }

                IReadOnlyList<ValidationResult<LegacyOrder>>
                    validationResults =
                        _workerPool.Process(
                            orderBatch,
                            _orderValidator.ValidateAndClean,
                            cancellationToken);

                List<FailedRecord> failureBatch = new();
                List<Order> targetOrderBatch = new();

                for (int index = 0;
                     index < orderBatch.Count;
                     index++)
                {
                    LegacyOrder order = orderBatch[index];

                    ValidationResult<LegacyOrder> result =
                        validationResults[index];

                    if (!result.IsValid)
                    {
                        failedOrderCount++;

                        failureBatch.Add(
                            CreateValidationFailure(
                                migrationRun.Id,
                                "LegacyOrders",
                                order.LegacyId.ToString(),
                                order,
                                result.Issues));

                        continue;
                    }

                    if (!customerIdMap.TryGetValue(
                            order.LegacyCustomerId,
                            out Guid newCustomerId))
                    {
                        failedOrderCount++;

                        failureBatch.Add(
                            CreateFailure(
                                migrationRun.Id,
                                "LegacyOrders",
                                order.LegacyId.ToString(),
                                order,
                                "ORDER_CUSTOMER_NOT_FOUND",
                                "Referenced customer was not migrated."));

                        continue;
                    }

                    targetOrderBatch.Add(
                        new Order
                        {
                            Id = Guid.NewGuid(),
                            LegacyOrderId = order.LegacyId,
                            CustomerId = newCustomerId,
                            Amount = order.Amount,
                            OrderDateUtc =
                                order.OrderDate!.Value
                        });
                }

                await PersistFailuresAsync(
                    failureBatch,
                    cancellationToken);

                if (targetOrderBatch.Count > 0)
                {
                    _targetContext.Orders.AddRange(
                        targetOrderBatch);

                    await _targetContext.SaveChangesAsync(
                        cancellationToken);

                    migratedOrderCount +=
                        targetOrderBatch.Count;

                    DetachRange(targetOrderBatch);
                }

                lastOrderId = orderBatch[^1].LegacyId;
            }

            migrationRun.MigratedCustomerCount =
                deduplicationResult.Customers.Count;

            migrationRun.MigratedOrderCount =
                migratedOrderCount;

            migrationRun.DuplicateCustomerCount =
                deduplicationResult.DuplicateCount;

            migrationRun.FailedRecordCount =
                failedCustomerCount + failedOrderCount;

            ReconciliationReport report =
                _reconciliationService.CreateReport(
                    migrationRun,
                    failedCustomerCount,
                    failedOrderCount);

            _reconciliationService.EnsureBalanced(report);

            migrationRun.Status =
                MigrationStatus.Completed;

            migrationRun.CompletedAtUtc =
                DateTime.UtcNow;

            await _targetContext.SaveChangesAsync(
                cancellationToken);

            int persistedCustomerCount =
                await _targetContext.Customers.CountAsync(
                    cancellationToken);

            int persistedOrderCount =
                await _targetContext.Orders.CountAsync(
                    cancellationToken);

            int persistedFailureCount =
                await _targetContext.FailedRecords
                    .CountAsync(
                        record =>
                            record.MigrationRunId ==
                            migrationRun.Id,
                        cancellationToken);

            _reconciliationService.EnsurePersistedCounts(
                report,
                persistedCustomerCount,
                persistedOrderCount,
                persistedFailureCount);

            if (simulateFailure)
            {
                throw new InvalidOperationException(
                    "Simulated critical migration failure.");
            }

            await transaction.CommitAsync(
                cancellationToken);

            return report;
        }
        catch (Exception exception)
        {
            string errorMessage =
                exception.GetBaseException().Message;

            if (transaction is not null)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);
            }

            _targetContext.ChangeTracker.Clear();

            FailedRecord criticalFailure =
                CreateFailure(
                    migrationRun.Id,
                    "MigrationPipeline",
                    migrationRun.Id.ToString(),
                    new
                    {
                        ExceptionType =
                            exception.GetBaseException()
                                .GetType().Name,

                        Message = errorMessage
                    },
                    "MIGRATION_CRITICAL_ERROR",
                    errorMessage);

            await PersistFailuresAsync(
                new[] { criticalFailure },
                CancellationToken.None);

            MigrationRun storedRun =
                await _targetContext.MigrationRuns
                    .SingleAsync(
                        run => run.Id == migrationRun.Id,
                        CancellationToken.None);

            storedRun.Status = transaction is null
                ? MigrationStatus.Failed
                : MigrationStatus.RolledBack;

            storedRun.CompletedAtUtc =
                DateTime.UtcNow;

            storedRun.MigratedCustomerCount = 0;
            storedRun.MigratedOrderCount = 0;
            storedRun.DuplicateCustomerCount = 0;
            storedRun.ErrorMessage = errorMessage;

            storedRun.FailedRecordCount =
                await _targetContext.FailedRecords
                    .CountAsync(
                        record =>
                            record.MigrationRunId ==
                            migrationRun.Id,
                        CancellationToken.None);

            await _targetContext.SaveChangesAsync(
                CancellationToken.None);

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<List<LegacyCustomer>>
        LoadCustomerBatchAsync(
            long? lastLegacyId,
            CancellationToken cancellationToken)
    {
        IQueryable<LegacyCustomer> query =
            _legacyContext.LegacyCustomers
                .AsNoTracking();

        if (lastLegacyId.HasValue)
        {
            query = query.Where(customer =>
                customer.LegacyId > lastLegacyId.Value);
        }

        return await query
            .OrderBy(customer => customer.LegacyId)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<LegacyOrder>>
        LoadOrderBatchAsync(
            long? lastLegacyId,
            CancellationToken cancellationToken)
    {
        IQueryable<LegacyOrder> query =
            _legacyContext.LegacyOrders
                .AsNoTracking();

        if (lastLegacyId.HasValue)
        {
            query = query.Where(order =>
                order.LegacyId > lastLegacyId.Value);
        }

        return await query
            .OrderBy(order => order.LegacyId)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task PersistFailuresAsync(
        IReadOnlyCollection<FailedRecord> failures,
        CancellationToken cancellationToken)
    {
        if (failures.Count == 0)
        {
            return;
        }

        await using TargetDbContext auditContext =
            _targetContextFactory();

        auditContext.FailedRecords.AddRange(failures);

        await auditContext.SaveChangesAsync(
            cancellationToken);
    }

    private void DetachRange<TEntity>(
        IEnumerable<TEntity> entities)
        where TEntity : class
    {
        foreach (TEntity entity in entities)
        {
            _targetContext.Entry(entity).State =
                EntityState.Detached;
        }
    }

    private static FailedRecord CreateValidationFailure<T>(
        Guid migrationRunId,
        string sourceTable,
        string sourceRecordId,
        T sourceRecord,
        IReadOnlyList<ValidationIssue> issues)
    {
        return CreateFailure(
            migrationRunId,
            sourceTable,
            sourceRecordId,
            sourceRecord,
            string.Join(
                ",",
                issues.Select(issue =>
                    issue.RuleCode)),
            string.Join(
                " | ",
                issues.Select(issue =>
                    issue.Message)));
    }

    private static FailedRecord CreateFailure<T>(
        Guid migrationRunId,
        string sourceTable,
        string sourceRecordId,
        T sourceRecord,
        string ruleCode,
        string reason)
    {
        return new FailedRecord
        {
            MigrationRunId = migrationRunId,
            SourceTable = sourceTable,
            SourceRecordId = sourceRecordId,
            RuleCode = ruleCode,
            Reason = reason,

            RawData =
                JsonSerializer.Serialize(sourceRecord),

            FailedAtUtc = DateTime.UtcNow
        };
    }
}
