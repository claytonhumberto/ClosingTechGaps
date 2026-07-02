namespace ClosingTechGaps.Application.DTOs;

public record IndexStatusDto(int RowCount, bool IsSeeded);

public record NonClusteredDemoDto(
    string Category,
    string PlanWithout, string PlanWith,
    double TimeWithoutMs, double TimeWithMs,
    string CreateIndexSql, string QuerySql);

public record CoveringDemoDto(
    string Category,
    string PlanNonCovering, string PlanCovering,
    double TimeNonCoveringMs, double TimeCoveringMs,
    string NonCoveringDdl, string CoveringDdl, string QuerySql);

public record FilteredDemoDto(
    string Category,
    string PlanFullIndex, string PlanFilteredIndex,
    double TimeFullIndexMs, double TimeFilteredIndexMs,
    string FullIndexDdl, string FilteredIndexDdl, string QuerySql,
    int TotalRows, int InactiveRows);

public record UniqueIndexDemoDto(
    bool ConstraintEnforced,
    string DuplicateSku,
    string InsertSql,
    string ErrorMessage,
    string CreateIndexSql);

public record ClusteredDemoDto(
    string PkPlan, string ScanPlan,
    double TimePkMs, double TimeScanMs,
    string PkQuerySql, string ScanQuerySql);

public record FullTextDemoDto(
    string Term,
    string PlanLike, string PlanFts,
    double TimeLikeMs, double TimeFtsMs,
    string LikeQuerySql, string FtsQuerySql, string FtsDdl);
