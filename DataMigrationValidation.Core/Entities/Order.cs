namespace DataMigrationValidation.Core.Entities;

public class Order
{
    public Guid Id { get; set; }

    public long LegacyOrderId { get; set; }

    public Guid CustomerId { get; set; }

    public decimal Amount { get; set; }

    public DateTime OrderDateUtc { get; set; }
}