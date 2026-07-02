using ClosingTechGaps.Domain.Entities;

namespace ClosingTechGaps.Domain.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Customer>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Customer> customers, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<(IEnumerable<Customer> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<(IEnumerable<Customer> Items, int TotalCount, int RecordsLoadedIntoMemory)> GetPagedWithIQueryableAsync(int page, int pageSize, CancellationToken ct = default);
    Task<(IEnumerable<Customer> Items, int TotalCount, int RecordsLoadedIntoMemory)> GetPagedWithIEnumerableAsync(int page, int pageSize, CancellationToken ct = default);
    Task<(IEnumerable<Customer> Items, int TotalCount, int QueryCount)> GetWithLazyLoadingAsync(int page, int pageSize, CancellationToken ct = default);
    Task<(IEnumerable<Customer> Items, int TotalCount, int QueryCount)> GetWithEagerLoadingAsync(int page, int pageSize, CancellationToken ct = default);
}
