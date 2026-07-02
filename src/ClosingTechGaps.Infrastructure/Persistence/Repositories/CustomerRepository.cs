using ClosingTechGaps.Domain.Entities;
using ClosingTechGaps.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClosingTechGaps.Infrastructure.Persistence.Repositories;

public class CustomerRepository(AppDbContext context, QueryCounter queryCounter) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<Customer>> GetAllAsync(CancellationToken ct = default)
        => await context.Customers.ToListAsync(ct);

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
    {
        await context.Customers.AddAsync(customer, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<Customer> customers, CancellationToken ct = default)
    {
        await context.Customers.AddRangeAsync(customers, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        context.Customers.Update(customer);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IEnumerable<Customer> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Customers.OrderBy(c => c.Name);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IEnumerable<Customer> Items, int TotalCount, int RecordsLoadedIntoMemory)> GetPagedWithIQueryableAsync(int page, int pageSize, CancellationToken ct = default)
    {
        // IQueryable: the entire pipeline is translated to SQL.
        // Only `pageSize` rows actually travel from the DB to memory.
        IQueryable<Customer> query = context.Customers.OrderBy(c => c.Name);

        int total = await query.CountAsync(ct);

        List<Customer> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total, items.Count);
    }

    public async Task<(IEnumerable<Customer> Items, int TotalCount, int RecordsLoadedIntoMemory)> GetPagedWithIEnumerableAsync(int page, int pageSize, CancellationToken ct = default)
    {
        // IEnumerable: ToListAsync() forces EF to load EVERY row into memory first.
        // Ordering, Skip and Take then run in C# — not in the database.
        IEnumerable<Customer> allInMemory = await context.Customers.ToListAsync(ct);

        int loadedCount = allInMemory.Count();

        IEnumerable<Customer> items = allInMemory
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return (items, loadedCount, loadedCount);
    }

    public async Task<(IEnumerable<Customer> Items, int TotalCount, int QueryCount)> GetWithLazyLoadingAsync(int page, int pageSize, CancellationToken ct = default)
    {
        queryCounter.Reset();

        // Query 1: load customers only — Orders are NOT fetched yet
        queryCounter.Increment();
        var customers = await context.Customers
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        int total = await context.Customers.CountAsync(ct);
        queryCounter.Increment();

        // Queries 2..N+1: accessing .Orders on each customer triggers a separate DB hit
        foreach (var customer in customers)
        {
            _ = customer.Orders.Count; // lazy load fires here — one hit per customer
            queryCounter.Increment();
        }

        return (customers, total, queryCounter.Count);
    }

    public async Task<(IEnumerable<Customer> Items, int TotalCount, int QueryCount)> GetWithEagerLoadingAsync(int page, int pageSize, CancellationToken ct = default)
    {
        queryCounter.Reset();

        // Query 1: single query — EF generates a JOIN and loads Orders together
        queryCounter.Increment();
        var customers = await context.Customers
            .Include(c => c.Orders)
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        int total = await context.Customers.CountAsync(ct);
        queryCounter.Increment();

        // .Orders is already populated — zero extra queries
        foreach (var customer in customers)
        {
            _ = customer.Orders.Count; // already loaded, no DB hit
        }

        return (customers, total, queryCounter.Count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await context.Customers.FindAsync([id], ct);
        if (customer is not null)
        {
            context.Customers.Remove(customer);
            await context.SaveChangesAsync(ct);
        }
    }
}
