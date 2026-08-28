using System.Diagnostics;
using DataMigrationValidation.Core.Deduplication;
using DataMigrationValidation.Core.Entities;
using DataMigrationValidation.Core.Enums;
using DataMigrationValidation.Core.Reports;
using DataMigrationValidation.Core.Validation;
using DataMigrationValidation.Infrastructure.Data;
using DataMigrationValidation.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

const int sourceCustomerCount = 20_000;
const int sourceOrderCount = 80_000;
const int batchSize = 500;
const int workerCount = 4;

const int expectedMigratedCustomerCount = 16_000;
const int expectedDuplicateCustomerCount = 2_000;
const int expectedFailedCustomerCount = 2_000;
const int expectedMigratedOrderCount = 72_000;
const int expectedFailedOrderCount = 8_000;
const int expectedFailedRecordCount =
    expectedFailedCustomerCount + expectedFailedOrderCount;

string testSuffix =
    Guid.NewGuid().ToString("N")[..8];

string legacyDatabaseName =
    $"DataMigrationLegacyLoadTest_{testSuffix}";

string targetDatabaseName =
    $"DataMigrationTargetLoadTest_{testSuffix}";

string legacyConnection =
    $@"Server=.\SQLEXPRESS;Database={legacyDatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";

string targetConnection =
    $@"Server=.\SQLEXPRESS;Database={targetDatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";

DbContextOptions<LegacyDbContext> legacyOptions =
    new DbContextOptionsBuilder<LegacyDbContext>()
        .UseSqlServer(legacyConnection)
        .Options;

DbContextOptions<TargetDbContext> targetOptions =
    new DbContextOptionsBuilder<TargetDbContext>()
        .UseSqlServer(targetConnection)
        .Options;

bool keepDatabases = args.Contains(
    "--keep-databases",
    StringComparer.OrdinalIgnoreCase);

try
{
    Console.WriteLine(
        $"Preparing {sourceCustomerCount:N0} customers and {sourceOrderCount:N0} orders...");

    await RunLoadTestAsync(
        legacyOptions,
        targetOptions);

    Console.WriteLine("LARGE DATA TEST PASSED");
}
finally
{
    if (!keepDatabases)
    {
        await DeleteTestDatabasesAsync(
            legacyOptions,
            targetOptions);

        Console.WriteLine(
            "Temporary load-test databases deleted.");
    }
    else
    {
        Console.WriteLine(
            $"Test databases kept: {legacyDatabaseName}, {targetDatabaseName}");
    }
}

static async Task RunLoadTestAsync(
    DbContextOptions<LegacyDbContext> legacyOptions,
    DbContextOptions<TargetDbContext> targetOptions)
{
    await using LegacyDbContext legacyContext =
        new(legacyOptions);

    await using TargetDbContext targetContext =
        new(targetOptions);

    await legacyContext.Database.EnsureCreatedAsync();
    await targetContext.Database.EnsureCreatedAsync();

    Stopwatch seedStopwatch = Stopwatch.StartNew();

    await SeedCustomersAsync(legacyContext);
    await SeedOrdersAsync(legacyContext);

    seedStopwatch.Stop();

    Console.WriteLine(
        $"Seed completed in {seedStopwatch.Elapsed.TotalSeconds:F1}s.");

    MigrationExecutionOptions executionOptions =
        new(batchSize, workerCount);

    MigrationPipeline pipeline = new(
        legacyContext,
        targetContext,
        () => new TargetDbContext(targetOptions),
        new LegacyCustomerValidator(),
        new LegacyOrderValidator(),
        new CustomerDeduplicator(),
        new ReconciliationService(),
        executionOptions);

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long managedMemoryBefore =
        GC.GetTotalMemory(true);

    Stopwatch migrationStopwatch =
        Stopwatch.StartNew();

    ReconciliationReport report =
        await pipeline.RunAsync();

    migrationStopwatch.Stop();

    long managedMemoryAfter =
        GC.GetTotalMemory(false);

    EnsureEqual(
        sourceCustomerCount,
        report.SourceCustomerCount,
        "source customer count");

    EnsureEqual(
        sourceOrderCount,
        report.SourceOrderCount,
        "source order count");

    EnsureEqual(
        expectedMigratedCustomerCount,
        report.MigratedCustomerCount,
        "migrated customer count");

    EnsureEqual(
        expectedDuplicateCustomerCount,
        report.DuplicateCustomerCount,
        "duplicate customer count");

    EnsureEqual(
        expectedMigratedOrderCount,
        report.MigratedOrderCount,
        "migrated order count");

    EnsureEqual(
        expectedFailedRecordCount,
        report.FailedRecordCount,
        "failed record count");

    EnsureEqual(true, report.IsBalanced, "balanced result");

    MigrationRun successfulRun =
        await targetContext.MigrationRuns
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstAsync();

    await AssertPersistedStateAsync(
        targetContext,
        successfulRun.Id,
        expectedMigratedCustomerCount,
        expectedMigratedOrderCount,
        expectedFailedRecordCount);

    Console.WriteLine(
        $"Normal migration completed in {migrationStopwatch.Elapsed.TotalSeconds:F1}s.");

    Console.WriteLine(
        $"Managed memory delta: {ToMegabytes(managedMemoryAfter - managedMemoryBefore):F1} MB.");

    Stopwatch rollbackStopwatch =
        Stopwatch.StartNew();

    bool simulatedFailureObserved = false;

    try
    {
        await pipeline.RunAsync(simulateFailure: true);
    }
    catch (InvalidOperationException exception)
        when (exception.GetBaseException().Message ==
              "Simulated critical migration failure.")
    {
        simulatedFailureObserved = true;
    }

    rollbackStopwatch.Stop();

    EnsureEqual(
        true,
        simulatedFailureObserved,
        "simulated rollback exception");

    targetContext.ChangeTracker.Clear();

    MigrationRun rolledBackRun =
        await targetContext.MigrationRuns
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstAsync();

    EnsureEqual(
        MigrationStatus.RolledBack,
        rolledBackRun.Status,
        "rollback status");

    EnsureEqual(
        expectedFailedRecordCount + 1,
        rolledBackRun.FailedRecordCount,
        "rollback audit count");

    await AssertPersistedStateAsync(
        targetContext,
        rolledBackRun.Id,
        expectedMigratedCustomerCount,
        expectedMigratedOrderCount,
        expectedFailedRecordCount + 1);

    Console.WriteLine(
        $"Rollback migration completed in {rollbackStopwatch.Elapsed.TotalSeconds:F1}s.");
}

static async Task SeedCustomersAsync(
    LegacyDbContext context)
{
    context.ChangeTracker.AutoDetectChangesEnabled = false;

    for (int start = 1;
         start <= sourceCustomerCount;
         start += batchSize)
    {
        int end = Math.Min(
            start + batchSize - 1,
            sourceCustomerCount);

        List<LegacyCustomer> customers =
            new(end - start + 1);

        for (int id = start; id <= end; id++)
        {
            long identitySource = id % 10 == 9
                ? id - 1
                : id;

            customers.Add(
                new LegacyCustomer
                {
                    LegacyId = id,
                    NationalIdentityNumber =
                        identitySource.ToString("D11"),
                    FullName = $"  Load   Customer {id}  ",
                    Email = id % 10 == 0
                        ? "invalid-email"
                        : $"CUSTOMER{id}@EXAMPLE.COM ",
                    Phone = "0555 123 45 67",
                    CreatedAt =
                        new DateTime(2024, 1, 1)
                            .AddMinutes(id)
                });
        }

        context.LegacyCustomers.AddRange(customers);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}

static async Task SeedOrdersAsync(
    LegacyDbContext context)
{
    context.ChangeTracker.AutoDetectChangesEnabled = false;

    for (int start = 1;
         start <= sourceOrderCount;
         start += batchSize)
    {
        int end = Math.Min(
            start + batchSize - 1,
            sourceOrderCount);

        List<LegacyOrder> orders =
            new(end - start + 1);

        for (int id = start; id <= end; id++)
        {
            long customerId =
                ((id - 1) % sourceCustomerCount) + 1;

            orders.Add(
                new LegacyOrder
                {
                    LegacyId = id,
                    LegacyCustomerId = customerId,
                    Amount = customerId % 20 == 0
                        ? -1m
                        : 100m + id % 500,
                    OrderDate =
                        new DateTime(2025, 1, 1)
                            .AddMinutes(id)
                });
        }

        context.LegacyOrders.AddRange(orders);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}

static async Task AssertPersistedStateAsync(
    TargetDbContext context,
    Guid migrationRunId,
    int expectedCustomerCount,
    int expectedOrderCount,
    int expectedRunFailureCount)
{
    int actualCustomerCount =
        await context.Customers.CountAsync();

    int actualOrderCount =
        await context.Orders.CountAsync();

    int actualRunFailureCount =
        await context.FailedRecords.CountAsync(record =>
            record.MigrationRunId == migrationRunId);

    EnsureEqual(
        expectedCustomerCount,
        actualCustomerCount,
        "persisted customer count");

    EnsureEqual(
        expectedOrderCount,
        actualOrderCount,
        "persisted order count");

    EnsureEqual(
        expectedRunFailureCount,
        actualRunFailureCount,
        "persisted run failure count");
}

static async Task DeleteTestDatabasesAsync(
    DbContextOptions<LegacyDbContext> legacyOptions,
    DbContextOptions<TargetDbContext> targetOptions)
{
    await using TargetDbContext targetContext =
        new(targetOptions);

    await targetContext.Database.EnsureDeletedAsync();

    await using LegacyDbContext legacyContext =
        new(legacyOptions);

    await legacyContext.Database.EnsureDeletedAsync();
}

static void EnsureEqual<T>(
    T expected,
    T actual,
    string name)
{
    if (!EqualityComparer<T>.Default.Equals(
            expected,
            actual))
    {
        throw new InvalidOperationException(
            $"Assertion failed for {name}: expected {expected}, actual {actual}.");
    }
}

static double ToMegabytes(long bytes)
{
    return bytes / 1024d / 1024d;
}
