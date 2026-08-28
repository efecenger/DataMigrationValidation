namespace DataMigrationValidation.Core.Entities;

public class Customer
{
    public Guid Id { get; set; }

    public string? NationalIdentityNumber { get; set; }

    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required string Phone { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}