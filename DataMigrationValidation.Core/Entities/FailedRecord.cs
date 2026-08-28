namespace DataMigrationValidation.Core.Entities;

public class FailedRecord
{
    public long Id { get; set; }

    public Guid MigrationRunId { get; set; }

    public required string SourceTable { get; set; }

    public required string SourceRecordId { get; set; }

    public required string RuleCode { get; set; }

    public required string Reason { get; set; }

    public required string RawData { get; set; }

    public DateTime FailedAtUtc { get; set; }
}