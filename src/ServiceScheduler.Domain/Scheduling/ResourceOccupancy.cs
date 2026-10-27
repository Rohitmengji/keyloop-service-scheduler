namespace ServiceScheduler.Domain.Scheduling;

public enum ResourceKind
{
    ServiceBay = 1,
    Technician = 2,
    Vehicle = 3,
}

/// <summary>One resource during one slot: the unit that can never be handed out twice.</summary>
public readonly record struct ResourceSlot(ResourceKind Kind, Guid ResourceId, DateTimeOffset SlotStartUtc);

/// <summary>A point-in-time snapshot of reserved slots, used to pick free resources without further queries.</summary>
public sealed class ResourceOccupancy(IEnumerable<ResourceSlot> reservedSlots)
{
    private readonly HashSet<ResourceSlot> _reserved = [.. reservedSlots];

    public bool IsFree(ResourceKind kind, Guid resourceId, TimeRange range) =>
        range.SlotStarts().All(slot => !_reserved.Contains(new ResourceSlot(kind, resourceId, slot)));
}
