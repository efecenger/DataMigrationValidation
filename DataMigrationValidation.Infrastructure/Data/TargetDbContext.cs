using DataMigrationValidation.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMigrationValidation.Infrastructure.Data;

public sealed class TargetDbContext : DbContext
{
    public TargetDbContext(
        DbContextOptions<TargetDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers
        => Set<Customer>();

    public DbSet<Order> Orders
        => Set<Order>();

    public DbSet<FailedRecord> FailedRecords
        => Set<FailedRecord>();

    public DbSet<MigrationRun> MigrationRuns
        => Set<MigrationRun>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(customer => customer.Id);

            entity.Property(customer => customer.FullName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(customer => customer.Email)
                .HasMaxLength(250)
                .IsRequired();

            entity.Property(customer => customer.Phone)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(customer =>
                    customer.NationalIdentityNumber)
                .HasMaxLength(11);

            entity.HasIndex(customer =>
                    customer.NationalIdentityNumber)
                .IsUnique()
                .HasFilter(
                    "[NationalIdentityNumber] IS NOT NULL");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(order => order.Id);

            entity.Property(order => order.Amount)
                .HasPrecision(18, 2);

            entity.HasIndex(order => order.LegacyOrderId)
                .IsUnique();

            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(order => order.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FailedRecord>(entity =>
        {
            entity.ToTable("Failed_Records");

            entity.HasKey(record => record.Id);

            entity.Property(record => record.SourceTable)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(record => record.SourceRecordId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(record => record.RuleCode)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(record => record.Reason)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(record => record.RawData)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.HasOne<MigrationRun>()
                .WithMany()
                .HasForeignKey(record =>
                    record.MigrationRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MigrationRun>(entity =>
        {
            entity.HasKey(run => run.Id);

            entity.Property(run => run.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(run => run.ErrorMessage)
                .HasMaxLength(2000);
        });
    }
}