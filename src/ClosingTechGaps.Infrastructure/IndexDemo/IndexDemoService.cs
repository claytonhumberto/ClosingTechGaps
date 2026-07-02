using System.Diagnostics;
using ClosingTechGaps.Application.DTOs;
using Microsoft.Data.Sqlite;

namespace ClosingTechGaps.Infrastructure.IndexDemo;

public class IndexDemoService : IDisposable
{
    private readonly SqliteConnection _conn;
    private volatile bool _seeded;
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private readonly SemaphoreSlim _demoLock = new(1, 1);

    private static readonly string[] Categories =
        ["Electronics", "Clothing", "Food", "Books", "Sports", "Toys", "Garden", "Automotive"];

    public IndexDemoService()
    {
        _conn = new SqliteConnection("Data Source=indexdemo;Mode=Memory;Cache=Shared");
        _conn.Open();
        Exec(@"CREATE TABLE IF NOT EXISTS products (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            name        TEXT NOT NULL,
            category    TEXT NOT NULL,
            price       REAL NOT NULL,
            description TEXT NOT NULL,
            is_active   INTEGER NOT NULL DEFAULT 1,
            sku         TEXT NOT NULL,
            created_at  TEXT NOT NULL
        )");
    }

    // ── Seed ──────────────────────────────────────────────────────────────────

    public async Task<int> EnsureSeededAsync()
    {
        if (_seeded) return RowCount();
        await _seedLock.WaitAsync();
        try
        {
            if (_seeded) return RowCount();
            if (RowCount() == 0) await Task.Run(SeedProducts);
            _seeded = true;
        }
        finally { _seedLock.Release(); }
        return RowCount();
    }

    private void SeedProducts()
    {
        var rng = new Random(42);
        using var tx = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO products (name,category,price,description,is_active,sku,created_at)
                            VALUES ($n,$c,$p,$d,$a,$s,$dt)";
        var pn  = cmd.Parameters.Add("$n",  SqliteType.Text);
        var pc  = cmd.Parameters.Add("$c",  SqliteType.Text);
        var pp  = cmd.Parameters.Add("$p",  SqliteType.Real);
        var pd  = cmd.Parameters.Add("$d",  SqliteType.Text);
        var pa  = cmd.Parameters.Add("$a",  SqliteType.Integer);
        var ps  = cmd.Parameters.Add("$s",  SqliteType.Text);
        var pdt = cmd.Parameters.Add("$dt", SqliteType.Text);

        for (int i = 0; i < 100_000; i++)
        {
            var cat = Categories[rng.Next(Categories.Length)];
            pn.Value  = $"Product {i:D6} {cat}";
            pc.Value  = cat;
            pp.Value  = Math.Round(rng.NextDouble() * 999 + 1, 2);
            pd.Value  = $"High quality {cat.ToLower()} item with excellent features. Code REF-{i:D6}.";
            pa.Value  = rng.Next(10) > 2 ? 1 : 0;
            ps.Value  = $"SKU-{i:D8}";
            pdt.Value = DateTime.UtcNow.AddDays(-rng.Next(1000)).ToString("yyyy-MM-dd");
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private int RowCount()
    {
        using var c = _conn.CreateCommand();
        c.CommandText = "SELECT COUNT(*) FROM products";
        return (int)(long)(c.ExecuteScalar() ?? 0L);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Exec(string sql)
    {
        using var c = _conn.CreateCommand();
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    private string QueryPlan(string sql, params (string n, object v)[] p)
    {
        using var c = _conn.CreateCommand();
        c.CommandText = "EXPLAIN QUERY PLAN " + sql;
        foreach (var (n, v) in p) c.Parameters.AddWithValue(n, v);
        var rows = new List<string>();
        using var r = c.ExecuteReader();
        while (r.Read()) rows.Add(r.GetString(r.GetOrdinal("detail")));
        return string.Join("\n", rows);
    }

    private double TimeMs(string sql, int runs, params (string n, object v)[] p)
    {
        double best = double.MaxValue;
        for (int i = 0; i < runs; i++)
        {
            using var c = _conn.CreateCommand();
            c.CommandText = sql;
            foreach (var (n, v) in p) c.Parameters.AddWithValue(n, v);
            var sw = Stopwatch.StartNew();
            using var r = c.ExecuteReader();
            while (r.Read()) { }
            sw.Stop();
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }
        return Math.Round(best, 3);
    }

    // ── Demos ─────────────────────────────────────────────────────────────────

    public async Task<NonClusteredDemoDto> NonClusteredAsync(string category = "Electronics")
    {
        await _demoLock.WaitAsync();
        try
        {
            await EnsureSeededAsync();
            const string q = "SELECT id, name, price FROM products WHERE category = $cat ORDER BY price DESC LIMIT 20";

            Exec("DROP INDEX IF EXISTS idx_demo_nc");
            var planOff = QueryPlan(q, ("$cat", category));
            var timeOff = TimeMs(q, 5, ("$cat", category));

            Exec("CREATE INDEX idx_demo_nc ON products(category)");
            var planOn = QueryPlan(q, ("$cat", category));
            var timeOn = TimeMs(q, 5, ("$cat", category));

            return new NonClusteredDemoDto(
                Category: category,
                PlanWithout: planOff, PlanWith: planOn,
                TimeWithoutMs: timeOff, TimeWithMs: timeOn,
                CreateIndexSql: "CREATE INDEX idx_category ON products (category)",
                QuerySql: q.Replace("$cat", $"'{category}'"));
        }
        finally { _demoLock.Release(); }
    }

    public async Task<CoveringDemoDto> CoveringAsync(string category = "Electronics")
    {
        await _demoLock.WaitAsync();
        try
        {
            await EnsureSeededAsync();
            const string q = "SELECT name, price FROM products WHERE category = $cat AND is_active = 1 ORDER BY price DESC LIMIT 20";

            Exec("DROP INDEX IF EXISTS idx_demo_nc");
            Exec("DROP INDEX IF EXISTS idx_demo_cov");
            Exec("CREATE INDEX idx_demo_nc ON products(category)");
            var planNc = QueryPlan(q, ("$cat", category));
            var timeNc = TimeMs(q, 5, ("$cat", category));

            Exec("DROP INDEX IF EXISTS idx_demo_nc");
            Exec("CREATE INDEX idx_demo_cov ON products(category, is_active, name, price)");
            var planCov = QueryPlan(q, ("$cat", category));
            var timeCov = TimeMs(q, 5, ("$cat", category));

            Exec("DROP INDEX IF EXISTS idx_demo_cov");

            return new CoveringDemoDto(
                Category: category,
                PlanNonCovering: planNc, PlanCovering: planCov,
                TimeNonCoveringMs: timeNc, TimeCoveringMs: timeCov,
                NonCoveringDdl: "CREATE INDEX idx_nc ON products (category)",
                CoveringDdl: "CREATE INDEX idx_covering ON products (category, is_active, name, price)",
                QuerySql: q.Replace("$cat", $"'{category}'"));
        }
        finally { _demoLock.Release(); }
    }

    public async Task<FilteredDemoDto> FilteredAsync(string category = "Books")
    {
        await _demoLock.WaitAsync();
        try
        {
            await EnsureSeededAsync();
            const string q = "SELECT id, name, price FROM products WHERE category = $cat AND is_active = 1 LIMIT 20";

            Exec("DROP INDEX IF EXISTS idx_demo_full");
            Exec("DROP INDEX IF EXISTS idx_demo_filt");
            Exec("CREATE INDEX idx_demo_full ON products(category, is_active)");
            var planFull = QueryPlan(q, ("$cat", category));
            var timeFull = TimeMs(q, 5, ("$cat", category));

            Exec("DROP INDEX IF EXISTS idx_demo_full");
            Exec("CREATE INDEX idx_demo_filt ON products(category) WHERE is_active = 1");
            var planFilt = QueryPlan(q, ("$cat", category));
            var timeFilt = TimeMs(q, 5, ("$cat", category));

            Exec("DROP INDEX IF EXISTS idx_demo_filt");

            using var cnt = _conn.CreateCommand();
            cnt.CommandText = "SELECT COUNT(*) FROM products WHERE is_active = 0";
            var inactive = (int)(long)(cnt.ExecuteScalar() ?? 0L);

            return new FilteredDemoDto(
                Category: category,
                PlanFullIndex: planFull, PlanFilteredIndex: planFilt,
                TimeFullIndexMs: timeFull, TimeFilteredIndexMs: timeFilt,
                FullIndexDdl: "CREATE INDEX idx_full ON products (category, is_active)",
                FilteredIndexDdl: "CREATE INDEX idx_filtered ON products (category) WHERE is_active = 1",
                QuerySql: q.Replace("$cat", $"'{category}'"),
                TotalRows: RowCount(), InactiveRows: inactive);
        }
        finally { _demoLock.Release(); }
    }

    public async Task<UniqueIndexDemoDto> UniqueAsync()
    {
        await _demoLock.WaitAsync();
        try
        {
            await EnsureSeededAsync();
            Exec("DROP INDEX IF EXISTS idx_demo_sku");
            Exec("CREATE UNIQUE INDEX idx_demo_sku ON products(sku)");

            const string dupSku = "SKU-00000001";
            const string sql = $"INSERT INTO products (name, category, price, description, is_active, sku, created_at) " +
                               $"VALUES ('Duplicate Item', 'Test', 9.99, 'duplicate test row', 1, '{dupSku}', '2024-01-01')";
            string error = "";
            bool caught = false;
            try { Exec(sql); }
            catch (SqliteException ex) { caught = true; error = ex.Message; }

            return new UniqueIndexDemoDto(
                ConstraintEnforced: caught,
                DuplicateSku: dupSku,
                InsertSql: sql,
                ErrorMessage: error,
                CreateIndexSql: "CREATE UNIQUE INDEX idx_sku ON products (sku)");
        }
        finally { _demoLock.Release(); }
    }

    public async Task<ClusteredDemoDto> ClusteredAsync()
    {
        await _demoLock.WaitAsync();
        try
        {
            await EnsureSeededAsync();
            const string pkQ   = "SELECT id, name, price FROM products WHERE id = $id";
            const string scanQ = "SELECT id, name, price FROM products WHERE description LIKE $term LIMIT 1";

            var planPk   = QueryPlan(pkQ,   ("$id",   50_000));
            var timePk   = TimeMs(pkQ,   10, ("$id",   50_000));
            var planScan = QueryPlan(scanQ, ("$term", "%REF-050000%"));
            var timeScan = TimeMs(scanQ, 3,  ("$term", "%REF-050000%"));

            return new ClusteredDemoDto(
                PkPlan: planPk, ScanPlan: planScan,
                TimePkMs: timePk, TimeScanMs: timeScan,
                PkQuerySql:   pkQ.Replace("$id",   "50000"),
                ScanQuerySql: scanQ.Replace("$term", "'%REF-050000%'"));
        }
        finally { _demoLock.Release(); }
    }

    public async Task<FullTextDemoDto> FullTextAsync(string term = "excellent features")
    {
        await _demoLock.WaitAsync();
        try
        {
            await EnsureSeededAsync();
            const string likeQ = "SELECT id, name FROM products WHERE description LIKE $t LIMIT 10";
            var planLike = QueryPlan(likeQ, ("$t", $"%{term}%"));
            var timeLike = TimeMs(likeQ, 3, ("$t", $"%{term}%"));

            Exec("CREATE VIRTUAL TABLE IF NOT EXISTS products_fts USING fts5(name, description, content=products, content_rowid=id)");

            using var ftsCheck = _conn.CreateCommand();
            ftsCheck.CommandText = "SELECT COUNT(*) FROM products_fts";
            if ((long)(ftsCheck.ExecuteScalar() ?? 0L) == 0)
                Exec("INSERT INTO products_fts(products_fts) VALUES('rebuild')");

            const string ftsQ = "SELECT rowid, name FROM products_fts WHERE products_fts MATCH $t LIMIT 10";
            var planFts = QueryPlan(ftsQ, ("$t", term));
            var timeFts = TimeMs(ftsQ, 3, ("$t", term));

            return new FullTextDemoDto(
                Term: term,
                PlanLike: planLike, PlanFts: planFts,
                TimeLikeMs: timeLike, TimeFtsMs: timeFts,
                LikeQuerySql: likeQ.Replace("$t", $"'%{term}%'"),
                FtsQuerySql:  ftsQ.Replace("$t",  $"'{term}'"),
                FtsDdl: "CREATE VIRTUAL TABLE products_fts USING fts5(name, description, content=products, content_rowid=id)");
        }
        finally { _demoLock.Release(); }
    }

    public void Dispose() => _conn.Dispose();
}
