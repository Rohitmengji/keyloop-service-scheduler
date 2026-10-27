namespace ServiceScheduler.Domain.Dealerships;

public sealed class ServiceBay
{
    private ServiceBay()
    {
    }

    public ServiceBay(Guid id, Guid dealershipId, string name, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        DealershipId = dealershipId;
        Name = name;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Inactive bays (for example, under maintenance) are never allocated.</summary>
    public bool IsActive { get; private set; }
}
