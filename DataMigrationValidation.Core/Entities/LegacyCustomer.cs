namespace DataMigrationValidation.Core.Entities;

public class LegacyCustomer
{
    public long LegacyId { get; set; }

    public string? NationalIdentityNumber { get; set; }

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public DateTime? CreatedAt { get; set; }
}