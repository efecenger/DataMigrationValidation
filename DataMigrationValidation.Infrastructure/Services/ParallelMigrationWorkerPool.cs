namespace DataMigrationValidation.Infrastructure.Services;

public sealed class ParallelMigrationWorkerPool
{
    private readonly int _maxDegreeOfParallelism;

    public ParallelMigrationWorkerPool(
        int maxDegreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxDegreeOfParallelism,
            1);

        _maxDegreeOfParallelism =
            maxDegreeOfParallelism;
    }

    public IReadOnlyList<TResult> Process<TSource, TResult>(
        IReadOnlyList<TSource> records,
        Func<TSource, TResult> processor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(processor);

        TResult[] results = new TResult[records.Count];

        Parallel.For(
            0,
            records.Count,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism =
                    _maxDegreeOfParallelism
            },
            index =>
            {
                results[index] =
                    processor(records[index]);
            });

        return results;
    }
}
