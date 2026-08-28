namespace DataMigrationValidation.Core.Reports;

public sealed record ReconciliationReport(
    int SourceCustomerCount,
    int SourceOrderCount,
    int MigratedCustomerCount,
    int MigratedOrderCount,
    int DuplicateCustomerCount,
    int FailedCustomerCount,
    int FailedOrderCount)
{
    public int FailedRecordCount => FailedCustomerCount + FailedOrderCount;

    public bool IsBalanced =>
        SourceCustomerCount ==
        MigratedCustomerCount +
        DuplicateCustomerCount +
        FailedCustomerCount
        &&
        SourceOrderCount ==
        MigratedOrderCount +
        FailedOrderCount;
}