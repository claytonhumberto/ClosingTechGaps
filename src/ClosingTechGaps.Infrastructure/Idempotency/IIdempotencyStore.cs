namespace ClosingTechGaps.Infrastructure.Idempotency;

public record IdempotencyEntry(int StatusCode, string ContentType, string Body, DateTimeOffset CreatedAt);

public interface IIdempotencyStore
{
    bool TryGet(string key, out IdempotencyEntry? entry);
    void Set(string key, IdempotencyEntry entry);
    SemaphoreSlim GetLock(string key);
}
