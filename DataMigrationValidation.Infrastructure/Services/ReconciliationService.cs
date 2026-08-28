using DataMigrationValidation.Core.Entities;
using DataMigrationValidation.Core.Reports;

namespace DataMigrationValidation.Infrastructure.Services;

public sealed class ReconciliationService
{
    public ReconciliationReport CreateReport(
        MigrationRun migrationRun,
        int failedCustomerCount,
        int failedOrderCount)
    {
        ArgumentNullException.ThrowIfNull(migrationRun);

        return new ReconciliationReport(
            migrationRun.SourceCustomerCount,
            migrationRun.SourceOrderCount,
            migrationRun.MigratedCustomerCount,
            migrationRun.MigratedOrderCount,
            migrationRun.DuplicateCustomerCount,
            failedCustomerCount,
            failedOrderCount);
    }

    public void EnsureBalanced(ReconciliationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (!report.IsBalanced)
        {
            throw new InvalidOperationException(
                "Reconciliation failed: record counts do not match.");
        }
    }

    public void EnsurePersistedCounts(
        ReconciliationReport report,
        int persistedCustomerCount,
        int persistedOrderCount,
        int persistedFailureCount)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (persistedCustomerCount !=
            report.MigratedCustomerCount)
        {
            throw new InvalidOperationException(
                "Reconciliation failed: persisted customer count does not match the migration report.");
        }

        if (persistedOrderCount !=
            report.MigratedOrderCount)
        {
            throw new InvalidOperationException(
                "Reconciliation failed: persisted order count does not match the migration report.");
        }

        if (persistedFailureCount !=
            report.FailedRecordCount)
        {
            throw new InvalidOperationException(
                "Reconciliation failed: persisted failure count does not match the migration report.");
        }
    }
}
