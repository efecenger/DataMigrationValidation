using DataMigrationValidation.Core.Entities;

namespace DataMigrationValidation.Core.Deduplication;

public sealed class CustomerDeduplicator
{
    public CustomerDeduplicationAccumulator CreateAccumulator()
    {
        return new CustomerDeduplicationAccumulator();
    }

    public DeduplicationResult Deduplicate(
        IEnumerable<LegacyCustomer> sourceCustomers)
    {
        ArgumentNullException.ThrowIfNull(sourceCustomers);

        CustomerDeduplicationAccumulator accumulator =
            CreateAccumulator();

        foreach (LegacyCustomer customer in
                 sourceCustomers.OrderBy(customer =>
                     customer.LegacyId))
        {
            accumulator.Add(customer);
        }

        return accumulator.Complete();
    }
}
