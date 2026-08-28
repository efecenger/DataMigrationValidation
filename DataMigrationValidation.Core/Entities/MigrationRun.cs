using DataMigrationValidation.Core.Enums;

namespace DataMigrationValidation.Core.Entities;

public class MigrationRun
{
    public Guid Id { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public MigrationStatus Status { get; set; }

    public int SourceCustomerCount { get; set; }

    public int SourceOrderCount { get; set; }

    public int MigratedCustomerCount { get; set; }

    public int MigratedOrderCount { get; set; }

    public int DuplicateCustomerCount { get; set; }

    public int FailedRecordCount { get; set; }

    public string? ErrorMessage { get; set; }
}