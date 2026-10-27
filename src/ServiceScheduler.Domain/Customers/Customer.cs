namespace ServiceScheduler.Domain.Customers;

public sealed class Customer
{
    private Customer()
    {
    }

    public Customer(Guid id, string fullName, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Id = id;
        FullName = fullName;
        Email = email;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;
}
