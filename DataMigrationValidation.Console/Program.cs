using DataMigrationValidation.Core.Deduplication;
using DataMigrationValidation.Core.Entities;
using DataMigrationValidation.Core.Reports;
using DataMigrationValidation.Core.Validation;
using DataMigrationValidation.Infrastructure.Data;
using DataMigrationValidation.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

string legacyConnection =
    @"Server=.\SQLEXPRESS;Database=DataMigrationLegacyV2;Trusted_Connection=True;TrustServerCertificate=True;";

string targetConnection =
    @"Server=.\SQLEXPRESS;Database=DataMigrationTargetRollback2;Trusted_Connection=True;TrustServerCertificate=True;";

int batchSize = ReadPositiveOption(
    args,
    "--batch-size",
    500);

int workerCount = ReadPositiveOption(
    args,
    "--workers",
    Math.Max(
        1,
        Math.Min(Environment.ProcessorCount, 8)));

MigrationExecutionOptions executionOptions =
    new(batchSize, workerCount);

DbContextOptions<LegacyDbContext> legacyOptions =
    new DbContextOptionsBuilder<LegacyDbContext>()
        .UseSqlServer(legacyConnection)
        .Options;

DbContextOptions<TargetDbContext> targetOptions =
    new DbContextOptionsBuilder<TargetDbContext>()
        .UseSqlServer(targetConnection)
        .Options;

await using LegacyDbContext legacyContext =
    new(legacyOptions);

await using TargetDbContext targetContext =
    new(targetOptions);

LegacyDataSeeder seeder =
    new(legacyContext);

await seeder.SeedAsync();

await targetContext.Database.EnsureCreatedAsync();

LegacyCustomerValidator customerValidator = new();
LegacyOrderValidator orderValidator = new();
CustomerDeduplicator customerDeduplicator = new();
ReconciliationService reconciliationService = new();

MigrationPipeline pipeline = new(
    legacyContext,
    targetContext,
    () => new TargetDbContext(targetOptions),
    customerValidator,
    orderValidator,
    customerDeduplicator,
    reconciliationService,
    executionOptions);

bool simulateFailure = args.Contains(
    "--simulate-failure",
    StringComparer.OrdinalIgnoreCase);

Console.WriteLine($"Batch size: {batchSize}");
Console.WriteLine($"Parallel workers: {workerCount}");

try
{
    ReconciliationReport report =
        await pipeline.RunAsync(simulateFailure);

    Console.WriteLine("Migration completed.");
    Console.WriteLine(
        $"Source customers: {report.SourceCustomerCount}");
    Console.WriteLine(
        $"Migrated customers: {report.MigratedCustomerCount}");
    Console.WriteLine(
        $"Duplicate customers: {report.DuplicateCustomerCount}");
    Console.WriteLine(
        $"Source orders: {report.SourceOrderCount}");
    Console.WriteLine(
        $"Migrated orders: {report.MigratedOrderCount}");
    Console.WriteLine(
        $"Failed records: {report.FailedRecordCount}");
    Console.WriteLine(
        $"Balanced: {report.IsBalanced}");
}
catch (Exception exception)
{
    MigrationRun? lastRun =
        await targetContext.MigrationRuns
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefaultAsync();

    Console.WriteLine("Migration failed.");
    Console.WriteLine(
        $"Status: {lastRun?.Status}");
    Console.WriteLine(
        $"Error: {exception.GetBaseException().Message}");
}

static int ReadPositiveOption(
    string[] arguments,
    string optionName,
    int defaultValue)
{
    string prefix = $"{optionName}=";

    string? argument = arguments.FirstOrDefault(value =>
        value.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase));

    if (argument is null)
    {
        return defaultValue;
    }

    string rawValue = argument[prefix.Length..];

    if (int.TryParse(rawValue, out int value) &&
        value > 0)
    {
        return value;
    }

    throw new ArgumentException(
        $"{optionName} must be a positive integer.");
}
