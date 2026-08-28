using DataMigrationValidation.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataMigrationValidation.Infrastructure.Data;

public sealed class LegacyDataSeeder
{
    private readonly LegacyDbContext _context;

    public LegacyDataSeeder(
        LegacyDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.Database.EnsureCreatedAsync(
            cancellationToken);

        if (await _context.LegacyCustomers.AnyAsync(
                cancellationToken))
        {
            return;
        }

        _context.LegacyCustomers.AddRange(
            new LegacyCustomer
            {
                LegacyId = 1,
                NationalIdentityNumber = "11111111110",
                FullName = "  Efe   Cenger ",
                Email = "EFE.CENGER@EXAMPLE.COM ",
                Phone = "0532 111 22 33",
                CreatedAt = new DateTime(2024, 1, 10)
            },
            new LegacyCustomer
            {
                LegacyId = 2,
                NationalIdentityNumber = "11111111110",
                FullName = "Efe Cenger",
                Email = "efe.cenger@example.com",
                Phone = "+90 (532) 111 22 33",
                CreatedAt = new DateTime(2024, 2, 5)
            },
            new LegacyCustomer
            {
                LegacyId = 3,
                FullName = "Ayse Yilmaz",
                Email = "broken-email",
                Phone = "123",
                CreatedAt = new DateTime(2024, 3, 12)
            },
            new LegacyCustomer
            {
                LegacyId = 4,
                FullName = "Mehmet Kaya",
                Email = "mehmet.kaya@example.com",
                Phone = "5554443322",
                CreatedAt = new DateTime(2024, 4, 1)
            },
            new LegacyCustomer
            {
                LegacyId = 5,
                FullName = null,
                Email = "noname@example.com",
                Phone = "0533 222 11 00",
                CreatedAt = new DateTime(2024, 5, 1)
            });

        _context.LegacyOrders.AddRange(
            new LegacyOrder
            {
                LegacyId = 101,
                LegacyCustomerId = 1,
                Amount = 1250.50m,
                OrderDate = new DateTime(2025, 1, 15)
            },
            new LegacyOrder
            {
                LegacyId = 102,
                LegacyCustomerId = 2,
                Amount = 850m,
                OrderDate = new DateTime(2025, 2, 18)
            },
            new LegacyOrder
            {
                LegacyId = 103,
                LegacyCustomerId = 4,
                Amount = -75m,
                OrderDate = new DateTime(2025, 3, 2)
            },
            new LegacyOrder
            {
                LegacyId = 104,
                LegacyCustomerId = 999,
                Amount = 300m,
                OrderDate = new DateTime(2025, 3, 9)
            },
            new LegacyOrder
            {
                LegacyId = 105,
                LegacyCustomerId = 5,
                Amount = 450m,
                OrderDate = new DateTime(2025, 3, 15)
            },
            new LegacyOrder
            {
                LegacyId = 106,
                LegacyCustomerId = 4,
                Amount = 600m,
                OrderDate = null
            });

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}