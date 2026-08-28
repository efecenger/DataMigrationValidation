namespace DataMigrationValidation.Core.Deduplication;

public sealed record DeduplicationResult(
    IReadOnlyList<DeduplicatedCustomer> Customers,
    int DuplicateCount);