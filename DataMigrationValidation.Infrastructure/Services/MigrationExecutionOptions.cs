namespace DataMigrationValidation.Infrastructure.Services;

public sealed class MigrationExecutionOptions
{
    public MigrationExecutionOptions(
        int batchSize,
        int maxDegreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            batchSize,
            1);

        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxDegreeOfParallelism,
            1);

        BatchSize = batchSize;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public int BatchSize { get; }

    public int MaxDegreeOfParallelism { get; }
}
