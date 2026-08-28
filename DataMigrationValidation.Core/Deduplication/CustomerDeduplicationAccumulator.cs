using DataMigrationValidation.Core.Entities;

namespace DataMigrationValidation.Core.Deduplication;

public sealed class CustomerDeduplicationAccumulator
{
    private readonly Dictionary<string, AccumulatedCustomer> _customers =
        new(StringComparer.OrdinalIgnoreCase);

    private int _sourceCount;

    public void Add(LegacyCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        string key = CreateKey(customer);

        if (_customers.TryGetValue(
                key,
                out AccumulatedCustomer? accumulatedCustomer))
        {
            accumulatedCustomer.Add(customer);
        }
        else
        {
            _customers[key] = new AccumulatedCustomer(customer);
        }

        _sourceCount++;
    }

    public DeduplicationResult Complete()
    {
        List<DeduplicatedCustomer> customers =
            _customers.Values
                .OrderBy(customer =>
                    customer.CanonicalRecord.LegacyId)
                .Select(customer =>
                    customer.ToDeduplicatedCustomer())
                .ToList();

        return new DeduplicationResult(
            customers,
            _sourceCount - customers.Count);
    }

    private static string CreateKey(
        LegacyCustomer customer)
    {
        if (!string.IsNullOrWhiteSpace(
                customer.NationalIdentityNumber))
        {
            return
                $"identity:{customer.NationalIdentityNumber}";
        }

        return
            $"email-name:{customer.Email}|{customer.FullName}";
    }

    private sealed class AccumulatedCustomer
    {
        private readonly List<long> _sourceLegacyIds = new();

        public AccumulatedCustomer(
            LegacyCustomer customer)
        {
            CanonicalRecord = Clone(customer);
            _sourceLegacyIds.Add(customer.LegacyId);
        }

        public LegacyCustomer CanonicalRecord { get; private set; }

        public void Add(LegacyCustomer customer)
        {
            CanonicalRecord = customer.LegacyId <
                              CanonicalRecord.LegacyId
                ? Merge(customer, CanonicalRecord)
                : Merge(CanonicalRecord, customer);

            _sourceLegacyIds.Add(customer.LegacyId);
        }

        public DeduplicatedCustomer ToDeduplicatedCustomer()
        {
            return new DeduplicatedCustomer(
                CanonicalRecord,
                _sourceLegacyIds.Order().ToArray());
        }

        private static LegacyCustomer Merge(
            LegacyCustomer earlierRecord,
            LegacyCustomer laterRecord)
        {
            return new LegacyCustomer
            {
                LegacyId = earlierRecord.LegacyId,

                NationalIdentityNumber =
                    FirstValue(
                        earlierRecord.NationalIdentityNumber,
                        laterRecord.NationalIdentityNumber),

                FullName =
                    FirstValue(
                        earlierRecord.FullName,
                        laterRecord.FullName),

                Email =
                    FirstValue(
                        earlierRecord.Email,
                        laterRecord.Email),

                Phone =
                    FirstValue(
                        earlierRecord.Phone,
                        laterRecord.Phone),

                CreatedAt =
                    EarliestDate(
                        earlierRecord.CreatedAt,
                        laterRecord.CreatedAt)
            };
        }

        private static LegacyCustomer Clone(
            LegacyCustomer customer)
        {
            return new LegacyCustomer
            {
                LegacyId = customer.LegacyId,
                NationalIdentityNumber =
                    customer.NationalIdentityNumber,
                FullName = customer.FullName,
                Email = customer.Email,
                Phone = customer.Phone,
                CreatedAt = customer.CreatedAt
            };
        }

        private static string? FirstValue(
            string? first,
            string? second)
        {
            return !string.IsNullOrWhiteSpace(first)
                ? first
                : second;
        }

        private static DateTime? EarliestDate(
            DateTime? first,
            DateTime? second)
        {
            if (!first.HasValue)
            {
                return second;
            }

            if (!second.HasValue)
            {
                return first;
            }

            return first.Value <= second.Value
                ? first
                : second;
        }
    }
}
