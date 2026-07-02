using ClosingTechGaps.Domain.Entities;
using ClosingTechGaps.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ClosingTechGaps.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
    }
}
