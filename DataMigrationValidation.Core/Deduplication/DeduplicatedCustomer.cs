using DataMigrationValidation.Core.Entities;

namespace DataMigrationValidation.Core.Deduplication;

public sealed record DeduplicatedCustomer(
    LegacyCustomer CanonicalRecord,
    IReadOnlyList<long> SourceLegacyIds);