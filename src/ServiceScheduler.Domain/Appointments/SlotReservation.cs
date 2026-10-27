using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Appointments;

public enum AppointmentStatus
{
    Confirmed = 1,
    Cancelled = 2,
}

/// <summary>
/// Claims one resource (bay, technician or vehicle) for one slot. The table's primary key is
/// (ResourceKind, ResourceId, SlotStartUtc), so two appointments can never hold the same claim.
/// </summary>
public sealed class SlotReservation
{
    private SlotReservation()
    {
    }

    internal SlotReservation(Guid appointmentId, ResourceKind resourceKind, Guid resourceId, DateTimeOffset slotStartUtc)
    {
        AppointmentId = appointmentId;
        ResourceKind = resourceKind;
        ResourceId = resourceId;
        SlotStartUtc = slotStartUtc;
    }

    public ResourceKind ResourceKind { get; private set; }

    public Guid ResourceId { get; private set; }

    public DateTimeOffset SlotStartUtc { get; private set; }

    public Guid AppointmentId { get; private set; }
}
