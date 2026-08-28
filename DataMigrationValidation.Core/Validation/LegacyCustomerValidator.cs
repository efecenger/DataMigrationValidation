using System.Text.RegularExpressions;
using DataMigrationValidation.Core.Cleaning;
using DataMigrationValidation.Core.Entities;

namespace DataMigrationValidation.Core.Validation;

public sealed class LegacyCustomerValidator
{
    private static readonly Regex EmailPattern =
        new(
            @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
            RegexOptions.Compiled);

    private static readonly Regex IdentityPattern =
        new(
            @"^[0-9]{11}$",
            RegexOptions.Compiled);

    public ValidationResult<LegacyCustomer> ValidateAndClean(
        LegacyCustomer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<ValidationIssue> issues = new();

        LegacyCustomer cleanedCustomer = new()
        {
            LegacyId = source.LegacyId,

            NationalIdentityNumber =
                DataCleaner.CleanNationalIdentityNumber(
                    source.NationalIdentityNumber),

            FullName =
                DataCleaner.CleanText(source.FullName),

            Email =
                DataCleaner.CleanEmail(source.Email),

            Phone =
                DataCleaner.CleanPhone(source.Phone),

            CreatedAt = source.CreatedAt
        };

        if (cleanedCustomer.LegacyId <= 0)
        {
            issues.Add(new ValidationIssue(
                "CUSTOMER_ID_INVALID",
                nameof(source.LegacyId),
                "Legacy customer id must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(
                cleanedCustomer.FullName))
        {
            issues.Add(new ValidationIssue(
                "CUSTOMER_NAME_REQUIRED",
                nameof(source.FullName),
                "Customer full name is required."));
        }

        if (string.IsNullOrWhiteSpace(
                cleanedCustomer.Email) ||
            !EmailPattern.IsMatch(cleanedCustomer.Email))
        {
            issues.Add(new ValidationIssue(
                "CUSTOMER_EMAIL_INVALID",
                nameof(source.Email),
                "Email address is missing or invalid."));
        }

        if (cleanedCustomer.Phone is null)
        {
            issues.Add(new ValidationIssue(
                "CUSTOMER_PHONE_INVALID",
                nameof(source.Phone),
                "Phone number is invalid."));
        }

        if (cleanedCustomer.NationalIdentityNumber
                is not null &&
            !IdentityPattern.IsMatch(
                cleanedCustomer.NationalIdentityNumber))
        {
            issues.Add(new ValidationIssue(
                "CUSTOMER_IDENTITY_INVALID",
                nameof(source.NationalIdentityNumber),
                "National identity number must contain 11 digits."));
        }

        return new ValidationResult<LegacyCustomer>(
            cleanedCustomer,
            issues);
    }
}