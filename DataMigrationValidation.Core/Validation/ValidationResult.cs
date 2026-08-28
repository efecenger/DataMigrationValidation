namespace DataMigrationValidation.Core.Validation;

public sealed class ValidationResult<T>
{
    public ValidationResult(
        T cleanedRecord,
        IReadOnlyList<ValidationIssue> issues)
    {
        CleanedRecord = cleanedRecord;
        Issues = issues;
    }

    public T CleanedRecord { get; }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;
}