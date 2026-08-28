using DataMigrationValidation.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMigrationValidation.Infrastructure.Data;

public sealed class LegacyDbContext : DbContext
{
    public LegacyDbContext(
        DbContextOptions<LegacyDbContext> options)
        : base(options)
    {
    }

    public DbSet<LegacyCustomer> LegacyCustomers =>
        Set<LegacyCustomer>();

    public DbSet<LegacyOrder> LegacyOrders =>
        Set<LegacyOrder>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LegacyCustomer>(entity =>
        {
            entity.HasKey(customer => customer.LegacyId);

            entity.Property(customer => customer.LegacyId)
                .ValueGeneratedNever();
        });

        modelBuilder.Entity<LegacyOrder>(entity =>
        {
            entity.HasKey(order => order.LegacyId);

            entity.Property(order => order.LegacyId)
                .ValueGeneratedNever();

            entity.Property(order => order.Amount)
                .HasPrecision(18, 2);
        });
    }
}