namespace ClosingTechGaps.Web.Models;

public record PagedWithMetricsViewModel<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    long ExecutionTimeMs,
    int RecordsLoadedIntoMemory,
    string Strategy,
    string StrategyExplanation
);
