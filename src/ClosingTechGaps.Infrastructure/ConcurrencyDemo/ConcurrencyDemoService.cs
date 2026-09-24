using Microsoft.Data.Sqlite;

namespace ClosingTechGaps.Infrastructure.ConcurrencyDemo;

public record DebitResult(bool Success, string Status, decimal Balance, string? PaymentId, bool Replayed, string Message);
public record LedgerEntryDto(string Id, decimal Amount, string Type, string ReferenceId, string CreatedAt);
public record AccountState(string AccountId, decimal Balance, IEnumerable<LedgerEntryDto> Ledger, int OutboxMessageCount);

public class ConcurrencyDemoService : IDisposable
{
    private const string ConnectionString = "Data Source=concurrency_demo;Mode=Memory;Cache=Shared";

    // A single connection is kept open for the process lifetime purely to keep the shared
    // in-memory database alive — SQLite drops a Mode=Memory;Cache=Shared database as soon as
    // its last connection closes. Every actual operation below opens its OWN short-lived
    // connection instead of reusing this one, because SqliteConnection is not safe to use
    // concurrently from multiple threads, and this demo's whole point is firing concurrent
    // requests at it. Separate connections against the same shared cache see the same data
    // and get SQLite's real locking behavior, which is exactly what the demo needs.
    private readonly SqliteConnection _keepAlive;

    public ConcurrencyDemoService()
    {
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Accounts (
                Id        TEXT PRIMARY KEY,
                Balance   REAL NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Payments (
                Id             TEXT PRIMARY KEY,
                AccountId      TEXT NOT NULL,
                Amount         REAL NOT NULL,
                Status         TEXT NOT NULL,
                CreatedAt      TEXT NOT NULL,
                IdempotencyKey TEXT UNIQUE
            );

            CREATE TABLE IF NOT EXISTS LedgerEntries (
                Id          TEXT PRIMARY KEY,
                AccountId   TEXT NOT NULL,
                Amount      REAL NOT NULL,
                Type        TEXT NOT NULL,
                ReferenceId TEXT NOT NULL,
                CreatedAt   TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS OutboxMessages (
                Id          TEXT PRIMARY KEY,
                Type        TEXT NOT NULL,
                Payload     TEXT NOT NULL,
                CreatedAt   TEXT NOT NULL,
                ProcessedAt TEXT NULL
            );
        """;
        cmd.ExecuteNonQuery();
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        // If two connections collide on a write lock, wait instead of throwing SQLITE_BUSY —
        // that lets the safe path serialize conflicting debits correctly under concurrency.
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync();
        return connection;
    }

    /// <summary>Resets the single demo account to a fresh starting balance and clears its history.</summary>
    public async Task<AccountState> ResetDemoAccountAsync(decimal startingBalance)
    {
        const string accountId = "demo-account";
        using var connection = await OpenConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM Payments WHERE AccountId = @accountId;
            DELETE FROM LedgerEntries WHERE AccountId = @accountId;
            DELETE FROM OutboxMessages;
            DELETE FROM Accounts WHERE Id = @accountId;
            INSERT INTO Accounts (Id, Balance, CreatedAt) VALUES (@accountId, @balance, @now);
        """;
        cmd.Parameters.AddWithValue("@accountId", accountId);
        cmd.Parameters.AddWithValue("@balance", startingBalance);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();

        return await GetStateAsync(accountId);
    }

    public async Task<AccountState> GetStateAsync(string accountId)
    {
        using var connection = await OpenConnectionAsync();

        decimal balance = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Balance FROM Accounts WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", accountId);
            var result = await cmd.ExecuteScalarAsync();
            if (result is not null) balance = Convert.ToDecimal(result);
        }

        var ledger = new List<LedgerEntryDto>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Amount, Type, ReferenceId, CreatedAt FROM LedgerEntries WHERE AccountId = @id ORDER BY CreatedAt";
            cmd.Parameters.AddWithValue("@id", accountId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                ledger.Add(new LedgerEntryDto(reader.GetString(0), Convert.ToDecimal(reader.GetDouble(1)), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }

        int outboxCount;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM OutboxMessages";
            outboxCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        return new AccountState(accountId, balance, ledger, outboxCount);
    }

    /// <summary>
    /// UNSAFE: read balance, check in application code, then write unconditionally.
    /// No transaction, no WHERE guard on the UPDATE — the article's "naive implementation".
    /// A short artificial delay between the read and the write makes the race condition
    /// reproduce reliably for the demo; in production the same race can happen from pure
    /// network/thread-scheduling jitter, without needing any delay at all.
    /// </summary>
    public async Task<DebitResult> DebitNaiveAsync(string accountId, decimal amount)
    {
        using var connection = await OpenConnectionAsync();

        decimal balance;
        using (var readCmd = connection.CreateCommand())
        {
            readCmd.CommandText = "SELECT Balance FROM Accounts WHERE Id = @id";
            readCmd.Parameters.AddWithValue("@id", accountId);
            var result = await readCmd.ExecuteScalarAsync();
            if (result is null) return new DebitResult(false, "AccountNotFound", 0, null, false, "Account not found.");
            balance = Convert.ToDecimal(result);
        }

        if (balance < amount)
            return new DebitResult(false, "InsufficientFunds", balance, null, false, $"Balance ${balance:0.00} is less than ${amount:0.00}.");

        // The gap between the read and the write is where a concurrent request can slip in.
        await Task.Delay(50);

        using (var writeCmd = connection.CreateCommand())
        {
            writeCmd.CommandText = "UPDATE Accounts SET Balance = Balance - @amount WHERE Id = @id";
            writeCmd.Parameters.AddWithValue("@amount", amount);
            writeCmd.Parameters.AddWithValue("@id", accountId);
            await writeCmd.ExecuteNonQueryAsync();
        }

        var newBalance = await GetBalanceAsync(connection, accountId);
        return new DebitResult(true, "Completed", newBalance, null, false, $"Debited ${amount:0.00} (unconditional write, balance checked before the delay).");
    }

    /// <summary>
    /// SAFE: idempotency-key fast path, then an atomic conditional UPDATE inside a transaction.
    /// The database — not application code — decides whether the debit is allowed.
    /// </summary>
    public async Task<DebitResult> DebitSafeAsync(string accountId, decimal amount, string idempotencyKey)
    {
        using var connection = await OpenConnectionAsync();

        var existing = await FindPaymentByIdempotencyKeyAsync(connection, idempotencyKey);
        if (existing is not null)
        {
            var balanceNow = await GetBalanceAsync(connection, accountId);
            return new DebitResult(true, existing.Value.Status, balanceNow, existing.Value.Id, true, "Replayed: this idempotency key was already processed.");
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            int rowsAffected;
            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = "UPDATE Accounts SET Balance = Balance - @amount WHERE Id = @id AND Balance >= @amount";
                updateCmd.Parameters.AddWithValue("@amount", amount);
                updateCmd.Parameters.AddWithValue("@id", accountId);
                rowsAffected = await updateCmd.ExecuteNonQueryAsync();
            }

            var paymentId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow.ToString("o");
            var status = rowsAffected == 1 ? "Completed" : "InsufficientFunds";

            using (var paymentCmd = connection.CreateCommand())
            {
                paymentCmd.Transaction = transaction;
                paymentCmd.CommandText = "INSERT INTO Payments (Id, AccountId, Amount, Status, CreatedAt, IdempotencyKey) VALUES (@id, @accountId, @amount, @status, @now, @key)";
                paymentCmd.Parameters.AddWithValue("@id", paymentId);
                paymentCmd.Parameters.AddWithValue("@accountId", accountId);
                paymentCmd.Parameters.AddWithValue("@amount", amount);
                paymentCmd.Parameters.AddWithValue("@status", status);
                paymentCmd.Parameters.AddWithValue("@now", now);
                paymentCmd.Parameters.AddWithValue("@key", idempotencyKey);
                await paymentCmd.ExecuteNonQueryAsync();
            }

            if (rowsAffected == 1)
            {
                using (var ledgerCmd = connection.CreateCommand())
                {
                    ledgerCmd.Transaction = transaction;
                    ledgerCmd.CommandText = "INSERT INTO LedgerEntries (Id, AccountId, Amount, Type, ReferenceId, CreatedAt) VALUES (@id, @accountId, @amount, 'Debit', @paymentId, @now)";
                    ledgerCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    ledgerCmd.Parameters.AddWithValue("@accountId", accountId);
                    ledgerCmd.Parameters.AddWithValue("@amount", -amount);
                    ledgerCmd.Parameters.AddWithValue("@paymentId", paymentId);
                    ledgerCmd.Parameters.AddWithValue("@now", now);
                    await ledgerCmd.ExecuteNonQueryAsync();
                }

                using (var outboxCmd = connection.CreateCommand())
                {
                    outboxCmd.Transaction = transaction;
                    outboxCmd.CommandText = "INSERT INTO OutboxMessages (Id, Type, Payload, CreatedAt) VALUES (@id, 'PaymentCompleted', @payload, @now)";
                    outboxCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    outboxCmd.Parameters.AddWithValue("@payload", $"{{\"paymentId\":\"{paymentId}\",\"accountId\":\"{accountId}\",\"amount\":{amount}}}");
                    outboxCmd.Parameters.AddWithValue("@now", now);
                    await outboxCmd.ExecuteNonQueryAsync();
                }
            }

            transaction.Commit();

            var balance = await GetBalanceAsync(connection, accountId);
            return rowsAffected == 1
                ? new DebitResult(true, "Completed", balance, paymentId, false, $"Debited ${amount:0.00} atomically. Payment {paymentId[..8]}...")
                : new DebitResult(false, "InsufficientFunds", balance, paymentId, false, $"Rejected by the database: balance ${balance:0.00} is less than ${amount:0.00}.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT — duplicate IdempotencyKey
        {
            transaction.Rollback();
            var replay = await FindPaymentByIdempotencyKeyAsync(connection, idempotencyKey);
            var balance = await GetBalanceAsync(connection, accountId);
            return new DebitResult(true, replay?.Status ?? "Completed", balance, replay?.Id, true, "Replayed: a concurrent request with the same idempotency key already committed first.");
        }
    }

    /// <summary>
    /// A reversal is not a rollback: it happens after the original payment already committed,
    /// and it leaves the original record untouched, adding a new offsetting ledger entry instead.
    /// </summary>
    public async Task<DebitResult> ReverseAsync(string paymentId)
    {
        using var connection = await OpenConnectionAsync();

        string? accountId = null;
        decimal amount = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT AccountId, Amount FROM Payments WHERE Id = @id AND Status = 'Completed'";
            cmd.Parameters.AddWithValue("@id", paymentId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                accountId = reader.GetString(0);
                amount = Convert.ToDecimal(reader.GetDouble(1));
            }
        }

        if (accountId is null)
            return new DebitResult(false, "PaymentNotFound", 0, null, false, "No completed payment with that id.");

        using var transaction = connection.BeginTransaction();
        var now = DateTime.UtcNow.ToString("o");

        using (var creditCmd = connection.CreateCommand())
        {
            creditCmd.Transaction = transaction;
            creditCmd.CommandText = "UPDATE Accounts SET Balance = Balance + @amount WHERE Id = @id";
            creditCmd.Parameters.AddWithValue("@amount", amount);
            creditCmd.Parameters.AddWithValue("@id", accountId);
            await creditCmd.ExecuteNonQueryAsync();
        }

        using (var ledgerCmd = connection.CreateCommand())
        {
            ledgerCmd.Transaction = transaction;
            ledgerCmd.CommandText = "INSERT INTO LedgerEntries (Id, AccountId, Amount, Type, ReferenceId, CreatedAt) VALUES (@id, @accountId, @amount, 'Reversal', @paymentId, @now)";
            ledgerCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            ledgerCmd.Parameters.AddWithValue("@accountId", accountId);
            ledgerCmd.Parameters.AddWithValue("@amount", amount);
            ledgerCmd.Parameters.AddWithValue("@paymentId", paymentId);
            ledgerCmd.Parameters.AddWithValue("@now", now);
            await ledgerCmd.ExecuteNonQueryAsync();
        }

        transaction.Commit();

        var balance = await GetBalanceAsync(connection, accountId);
        return new DebitResult(true, "Reversed", balance, paymentId, false, $"Reversed ${amount:0.00}. Original payment record is unchanged; a new +${amount:0.00} ledger entry was added.");
    }

    private static async Task<decimal> GetBalanceAsync(SqliteConnection connection, string accountId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Balance FROM Accounts WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", accountId);
        var result = await cmd.ExecuteScalarAsync();
        return result is null ? 0 : Convert.ToDecimal(result);
    }

    private static async Task<(string Id, string Status)?> FindPaymentByIdempotencyKeyAsync(SqliteConnection connection, string idempotencyKey)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Status FROM Payments WHERE IdempotencyKey = @key";
        cmd.Parameters.AddWithValue("@key", idempotencyKey);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    public void Dispose() => _keepAlive.Dispose();
}
