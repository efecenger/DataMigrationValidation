namespace DataMigrationValidation.Core.Cleaning;

public static class DataCleaner
{
    public static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(
            ' ',
            value.Trim().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));
    }

    public static string? CleanEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    public static string? CleanNationalIdentityNumber(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new string(
                value.Where(char.IsDigit).ToArray());
    }

    public static string? CleanPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string digits = new(
            value.Where(char.IsDigit).ToArray());

        if (digits.Length == 11 &&
            digits.StartsWith("0"))
        {
            digits = $"90{digits[1..]}";
        }
        else if (digits.Length == 10)
        {
            digits = $"90{digits}";
        }

        return digits.Length == 12 &&
               digits.StartsWith("90")
            ? $"+{digits}"
            : null;
    }
}