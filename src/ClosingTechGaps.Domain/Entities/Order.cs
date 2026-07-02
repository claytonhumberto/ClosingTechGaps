namespace ClosingTechGaps.Domain.Entities;

public class Order
{
    public Guid Id { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public string Description { get; protected set; } = string.Empty;
    public decimal Amount { get; protected set; }
    public DateTime CreatedAt { get; protected set; }

    public virtual Customer Customer { get; protected set; } = default!;

    protected Order() { }

    public Order(Guid customerId, string description, decimal amount, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Description = description;
        Amount = amount;
        CreatedAt = createdAt;
    }
}
