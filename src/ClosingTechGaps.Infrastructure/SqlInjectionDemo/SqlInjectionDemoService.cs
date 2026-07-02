using Microsoft.Data.Sqlite;

namespace ClosingTechGaps.Infrastructure.SqlInjectionDemo;

public record DemoCustomer(int Id, string Name, string Email, string Role);

public record SqlDemoResult(
    string SqlExecuted,
    IEnumerable<DemoCustomer> Records,
    bool DataLeaked,
    string Warning
);

public class SqlInjectionDemoService : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqlInjectionDemoService()
    {
        // Shared in-memory SQLite — kept alive for the lifetime of this scoped service
        _connection = new SqliteConnection("Data Source=sqli_demo;Mode=Memory;Cache=Shared");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();

        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Customers (
                Id    INTEGER PRIMARY KEY AUTOINCREMENT,
                Name  TEXT NOT NULL,
                Email TEXT NOT NULL,
                Role  TEXT NOT NULL
            );
        """;
        cmd.ExecuteNonQuery();

        // Seed only if empty
        cmd.CommandText = "SELECT COUNT(*) FROM Customers";
        var count = (long)(cmd.ExecuteScalar() ?? 0L);
        if (count > 0) return;

        cmd.CommandText = """
            INSERT INTO Customers (Name, Email, Role) VALUES
              ('Alice Johnson',   'alice@example.com',   'customer'),
              ('Bob Smith',       'bob@example.com',     'customer'),
              ('Carol Williams',  'carol@example.com',   'customer'),
              ('Admin User',      'admin@internal.com',  'admin'),
              ('System Account',  'system@internal.com', 'system');
        """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// UNSAFE: builds SQL via string concatenation — classic SQL Injection vector.
    /// Never do this in production code.
    /// </summary>
    public SqlDemoResult SearchUnsafe(string userInput)
    {
        // The attacker controls `userInput` — it is pasted directly into the SQL
        var sql = $"SELECT Id, Name, Email, Role FROM Customers WHERE Name LIKE '%{userInput}%'";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        var records = ReadCustomers(cmd);
        bool leaked = records.Any(r => r.Role is "admin" or "system");

        string warning = leaked
            ? "BREACH: privileged records exposed. The injected input escaped the intended query boundary."
            : records.Any()
                ? "Query succeeded — no privileged data exposed this time, but the vector is open."
                : "No records returned — but the injection vector is still present.";

        return new SqlDemoResult(sql, records, leaked, warning);
    }

    /// <summary>
    /// SAFE: uses a parameterized SqliteParameter — the driver escapes the value,
    /// the query structure can never be altered by user input.
    /// </summary>
    public SqlDemoResult SearchSafe(string userInput)
    {
        const string sql = "SELECT Id, Name, Email, Role FROM Customers WHERE Name LIKE @name";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@name", $"%{userInput}%");

        var records = ReadCustomers(cmd);

        return new SqlDemoResult(
            SqlExecuted: sql + $"   -- @name = '%{userInput}%'",
            Records: records,
            DataLeaked: false,
            Warning: "Parameterized query: user input is bound as a value, never interpreted as SQL syntax."
        );
    }

    private static List<DemoCustomer> ReadCustomers(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<DemoCustomer>();
        while (reader.Read())
            list.Add(new DemoCustomer(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)
            ));
        return list;
    }

    public void Dispose() => _connection.Dispose();
}
