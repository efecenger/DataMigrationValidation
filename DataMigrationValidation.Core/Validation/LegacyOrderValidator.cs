using DataMigrationValidation.Core.Entities;

namespace DataMigrationValidation.Core.Validation;

public sealed class LegacyOrderValidator
{
    public ValidationResult<LegacyOrder> ValidateAndClean(
        LegacyOrder source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<ValidationIssue> issues = new();

        if (source.LegacyId <= 0)
        {
            issues.Add(new ValidationIssue(
                "ORDER_ID_INVALID",
                nameof(source.LegacyId),
                "Legacy order id must be greater than zero."));
        }

        if (source.LegacyCustomerId <= 0)
        {
            issues.Add(new ValidationIssue(
                "ORDER_CUSTOMER_ID_INVALID",
                nameof(source.LegacyCustomerId),
                "Order must reference a valid customer id."));
        }

        if (source.Amount < 0)
        {
            issues.Add(new ValidationIssue(
                "ORDER_AMOUNT_NEGATIVE",
                nameof(source.Amount),
                "Order amount cannot be negative."));
        }

        if (source.OrderDate is null)
        {
            issues.Add(new ValidationIssue(
                "ORDER_DATE_REQUIRED",
                nameof(source.OrderDate),
                "Order date is required."));
        }

        return new ValidationResult<LegacyOrder>(
            source,
            issues);
    }
}