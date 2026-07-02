using ClosingTechGaps.Domain.ValueObjects;

namespace ClosingTechGaps.Domain.Entities;

public class Customer
{
    public Guid Id { get; protected set; }
    public string Name { get; protected set; } = string.Empty;
    public DateOnly BirthdayDate { get; protected set; }
    public Address Address { get; protected set; } = default!;
    public ContactInfo ContactInfo { get; protected set; } = default!;

    public virtual ICollection<Order> Orders { get; protected set; } = [];

    protected Customer() { }

    public Customer(string name, DateOnly birthdayDate, Address address, ContactInfo contactInfo)
    {
        Id = Guid.NewGuid();
        Name = name;
        BirthdayDate = birthdayDate;
        Address = address;
        ContactInfo = contactInfo;
    }

    public void Update(string name, DateOnly birthdayDate, Address address, ContactInfo contactInfo)
    {
        Name = name;
        BirthdayDate = birthdayDate;
        Address = address;
        ContactInfo = contactInfo;
    }
}
