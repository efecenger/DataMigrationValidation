namespace DataMigrationValidation.Core.Validation;

public sealed record ValidationIssue(
    string RuleCode,
    string Field,
    string Message);