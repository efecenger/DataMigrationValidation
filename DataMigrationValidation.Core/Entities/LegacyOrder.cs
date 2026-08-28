namespace DataMigrationValidation.Core.Entities;

public class LegacyOrder
{
    public long LegacyId { get; set; }

    public long LegacyCustomerId { get; set; }

    public decimal Amount { get; set; }

    public DateTime? OrderDate { get; set; }
}