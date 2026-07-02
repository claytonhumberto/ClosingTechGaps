namespace ClosingTechGaps.Web.Models;

public record OrderViewModel(Guid Id, string Description, decimal Amount, DateTime CreatedAt);

public record CustomerWithOrdersViewModel(
    Guid Id,
    string Name,
    DateOnly BirthdayDate,
    AddressViewModel Address,
    ContactInfoViewModel ContactInfo,
    IEnumerable<OrderViewModel> Orders
);

public record LoadingComparisonViewModel<T>(
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
