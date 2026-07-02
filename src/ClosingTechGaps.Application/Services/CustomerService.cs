using System.Diagnostics;
using ClosingTechGaps.Application.DTOs;
using ClosingTechGaps.Application.Interfaces;
using ClosingTechGaps.Domain.Entities;
using ClosingTechGaps.Domain.Repositories;
using ClosingTechGaps.Domain.ValueObjects;

namespace ClosingTechGaps.Application.Services;

public class CustomerService(ICustomerRepository repository) : ICustomerService
{
    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await repository.GetByIdAsync(id, ct);
        return customer is null ? null : ToDto(customer);
    }

    public async Task<IEnumerable<CustomerDto>> GetAllAsync(CancellationToken ct = default)
    {
        var customers = await repository.GetAllAsync(ct);
        return customers.Select(ToDto);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = new Customer(
            dto.Name,
            dto.BirthdayDate,
            new Address(dto.Address.Street, dto.Address.City, dto.Address.State, dto.Address.ZipCode, dto.Address.Country),
            new ContactInfo(dto.ContactInfo.Email, dto.ContactInfo.Phone)
        );

        await repository.AddAsync(customer, ct);
        return ToDto(customer);
    }

    public async Task UpdateAsync(Guid id, CreateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = await repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");

        customer.Update(
            dto.Name,
            dto.BirthdayDate,
            new Address(dto.Address.Street, dto.Address.City, dto.Address.State, dto.Address.ZipCode, dto.Address.Country),
            new ContactInfo(dto.ContactInfo.Email, dto.ContactInfo.Phone)
        );

        await repository.UpdateAsync(customer, ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => repository.DeleteAsync(id, ct);

    public async Task<PagedResultDto<CustomerDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await repository.GetPagedAsync(page, pageSize, ct);
        int totalPages = (int)Math.Ceiling(total / (double)pageSize);

        if (total > 0 && page > totalPages)
            throw new ArgumentOutOfRangeException(nameof(page), $"A página {page} não existe. Total de páginas: {totalPages}.");

        return new PagedResultDto<CustomerDto>(items.Select(ToDto), page, pageSize, total);
    }

    public async Task<PagedWithMetricsDto<CustomerDto>> GetPagedWithIQueryableAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var (items, total, loaded) = await repository.GetPagedWithIQueryableAsync(page, pageSize, ct);
        sw.Stop();

        int totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedWithMetricsDto<CustomerDto>(
            Items: items.Select(ToDto),
            Page: page,
            PageSize: pageSize,
            TotalCount: total,
            TotalPages: totalPages,
            HasPreviousPage: page > 1,
            HasNextPage: page < totalPages,
            ExecutionTimeMs: sw.ElapsedMilliseconds,
            RecordsLoadedIntoMemory: loaded,
            Strategy: "IQueryable",
            StrategyExplanation: $"The database received a single SQL with ORDER BY + OFFSET/FETCH. Only {loaded} rows were transferred from DB to memory."
        );
    }

    public async Task<PagedWithMetricsDto<CustomerDto>> GetPagedWithIEnumerableAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var (items, total, loaded) = await repository.GetPagedWithIEnumerableAsync(page, pageSize, ct);
        sw.Stop();

        int totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedWithMetricsDto<CustomerDto>(
            Items: items.Select(ToDto),
            Page: page,
            PageSize: pageSize,
            TotalCount: total,
            TotalPages: totalPages,
            HasPreviousPage: page > 1,
            HasNextPage: page < totalPages,
            ExecutionTimeMs: sw.ElapsedMilliseconds,
            RecordsLoadedIntoMemory: loaded,
            Strategy: "IEnumerable",
            StrategyExplanation: $"ALL {loaded} rows were loaded into memory first. Ordering, Skip and Take then ran in C# — the database did not filter anything."
        );
    }

    public async Task<LoadingComparisonDto<CustomerWithOrdersDto>> GetWithLazyLoadingAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var (items, total, queryCount) = await repository.GetWithLazyLoadingAsync(page, pageSize, ct);
        sw.Stop();

        int totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new LoadingComparisonDto<CustomerWithOrdersDto>(
            Items: items.Select(ToDtoWithOrders),
            Page: page, PageSize: pageSize, TotalCount: total, TotalPages: totalPages,
            HasPreviousPage: page > 1, HasNextPage: page < totalPages,
            ExecutionTimeMs: sw.ElapsedMilliseconds,
            QueriesExecuted: queryCount,
            Strategy: "Lazy Loading",
            StrategyExplanation: $"{queryCount} queries hit the database: 1 for customers + 1 for COUNT + 1 per customer to load Orders on demand. This is the N+1 problem."
        );
    }

    public async Task<LoadingComparisonDto<CustomerWithOrdersDto>> GetWithEagerLoadingAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var (items, total, queryCount) = await repository.GetWithEagerLoadingAsync(page, pageSize, ct);
        sw.Stop();

        int totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new LoadingComparisonDto<CustomerWithOrdersDto>(
            Items: items.Select(ToDtoWithOrders),
            Page: page, PageSize: pageSize, TotalCount: total, TotalPages: totalPages,
            HasPreviousPage: page > 1, HasNextPage: page < totalPages,
            ExecutionTimeMs: sw.ElapsedMilliseconds,
            QueriesExecuted: queryCount,
            Strategy: "Eager Loading",
            StrategyExplanation: $"Only {queryCount} queries hit the database: 1 with Include (JOIN) + 1 for COUNT. Orders were loaded alongside customers in a single round-trip."
        );
    }

    private static CustomerWithOrdersDto ToDtoWithOrders(Customer c) => new(
        c.Id, c.Name, c.BirthdayDate,
        new AddressDto(c.Address.Street, c.Address.City, c.Address.State, c.Address.ZipCode, c.Address.Country),
        new ContactInfoDto(c.ContactInfo.Email, c.ContactInfo.Phone),
        c.Orders.Select(o => new OrderDto(o.Id, o.Description, o.Amount, o.CreatedAt))
    );

    private static CustomerDto ToDto(Customer c) => new(
        c.Id,
        c.Name,
        c.BirthdayDate,
        new AddressDto(c.Address.Street, c.Address.City, c.Address.State, c.Address.ZipCode, c.Address.Country),
        new ContactInfoDto(c.ContactInfo.Email, c.ContactInfo.Phone)
    );
}
