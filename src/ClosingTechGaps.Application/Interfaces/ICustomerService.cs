using ClosingTechGaps.Application.DTOs;

namespace ClosingTechGaps.Application.Interfaces;

public interface ICustomerService
{
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<CustomerDto>> GetAllAsync(CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, CreateCustomerDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PagedResultDto<CustomerDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedWithMetricsDto<CustomerDto>> GetPagedWithIQueryableAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedWithMetricsDto<CustomerDto>> GetPagedWithIEnumerableAsync(int page, int pageSize, CancellationToken ct = default);
    Task<LoadingComparisonDto<CustomerWithOrdersDto>> GetWithLazyLoadingAsync(int page, int pageSize, CancellationToken ct = default);
    Task<LoadingComparisonDto<CustomerWithOrdersDto>> GetWithEagerLoadingAsync(int page, int pageSize, CancellationToken ct = default);
}
