namespace ClosingTechGaps.Infrastructure.Persistence;

public class QueryCounter
{
    public int Count { get; private set; }
    public void Increment() => Count++;
    public void Reset() => Count = 0;
}
