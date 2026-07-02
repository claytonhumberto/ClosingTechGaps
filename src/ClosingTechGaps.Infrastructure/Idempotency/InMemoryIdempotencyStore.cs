using System.Collections.Concurrent;

namespace ClosingTechGaps.Infrastructure.Idempotency;

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _store = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public bool TryGet(string key, out IdempotencyEntry? entry)
        => _store.TryGetValue(key, out entry);

    public void Set(string key, IdempotencyEntry entry)
        => _store[key] = entry;

    public SemaphoreSlim GetLock(string key)
        => _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
}
