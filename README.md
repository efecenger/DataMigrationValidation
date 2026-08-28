# Data Migration & Validation

A .NET application that safely migrates corrupted, incomplete, inconsistent, and duplicate customer and order records into a clean SQL Server schema.

## Features

- Validation of phone numbers, email addresses, identity numbers, monetary amounts, dates, and foreign-key references
- Normalization of text, phone numbers, email addresses, and identity numbers
- Customer deduplication by national identity number or email address plus full name
- A `Failed_Records` quarantine table for records that cannot be repaired automatically
- Transactional execution, simulated critical failures, and rollback support
- Reconciliation of source, target, and quarantined record counts
- Configurable batch processing with keyset pagination
- A bounded parallel migration worker pool
- An automated SQL Server load and rollback test covering 100,000 source records

## Requirements

- .NET 10 SDK
- SQL Server Express (`.\SQLEXPRESS`)
- `make` (optional)

The application creates its sample source and target databases through `EnsureCreated`.

## Running the migration

```powershell
make run
```

Alternatively:

```powershell
dotnet run --project DataMigrationValidation.Console -- --batch-size=500 --workers=4
```

## Testing rollback

```powershell
make rollback
```

Expected status:

```text
Status: RolledBack
Error: Simulated critical migration failure.
```

## Running the large-data test

```powershell
make load-test
```

The test generates 20,000 customers and 80,000 orders, then verifies the normal migration, persisted reconciliation counts, and rollback behavior. It uses uniquely named temporary databases and deletes them automatically after completion.

## Project structure

- `DataMigrationValidation.Core`: Entities, cleaning, validation, deduplication, and report models
- `DataMigrationValidation.Infrastructure`: EF Core contexts, migration pipeline, and worker infrastructure
- `DataMigrationValidation.Console`: Application entry point
- `DataMigrationValidation.LoadTests`: Large-data integration and load test
