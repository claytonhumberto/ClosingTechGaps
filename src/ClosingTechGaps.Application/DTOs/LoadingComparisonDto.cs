namespace ClosingTechGaps.Application.DTOs;

public record OrderDto(Guid Id, string Description, decimal Amount, DateTime CreatedAt);

public record CustomerWithOrdersDto(
    Guid Id,
    string Name,
    DateOnly BirthdayDate,
    AddressDto Address,
    ContactInfoDto ContactInfo,
    IEnumerable<OrderDto> Orders
);

public record LoadingComparisonDto<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    long ExecutionTimeMs,
    int QueriesExecuted,
    string Strategy,
    string StrategyExplanation
);
