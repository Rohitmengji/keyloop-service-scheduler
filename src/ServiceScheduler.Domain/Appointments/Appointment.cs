using ServiceScheduler.Domain.Catalog;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Appointments;

/// <summary>The parties and resources chosen for a booking.</summary>
public sealed record AppointmentAllocation(
    Dealership Dealership,
    Customer Customer,
    Vehicle Vehicle,
    ServiceType ServiceType,
    ServiceBay ServiceBay,
    Technician Technician,
    TimeRange Slot);

/// <summary>
/// Aggregate root for a service booking. It owns its slot reservations, so confirming creates every claim
/// and cancelling releases them in the same unit of work.
/// </summary>
public sealed class Appointment
{
    public const int NotesMaxLength = 500;
    public const int IdempotencyKeyMaxLength = 100;

    private readonly List<SlotReservation> _reservations = [];

    private Appointment()
    {
    }

    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid ServiceTypeId { get; private set; }

    public Guid ServiceBayId { get; private set; }

    public Guid TechnicianId { get; private set; }

    public DateTimeOffset StartUtc { get; private set; }

    public DateTimeOffset EndUtc { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? IdempotencyKey { get; private set; }

    /// <summary>SHA-256 of the normalised booking request, used to detect an idempotency key reused for different data.</summary>
    public string? RequestFingerprint { get; private set; }

    /// <summary>Optimistic concurrency token; bumped on every state change.</summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<SlotReservation> Reservations => _reservations;

    public static Appointment Confirm(
        Guid id,
        AppointmentAllocation allocation,
        DateTimeOffset createdAtUtc,
        string? notes = null,
        string? idempotencyKey = null,
        string? requestFingerprint = null)
    {
        EnsureAllocationIsValid(allocation, createdAtUtc);
        if (notes?.Length > NotesMaxLength)
        {
            throw new ArgumentException($"Notes cannot exceed {NotesMaxLength} characters.", nameof(notes));
        }

        if ((idempotencyKey is null) != (requestFingerprint is null))
        {
            throw new ArgumentException("An idempotency key and its request fingerprint must be supplied together.");
        }

        var appointment = new Appointment
        {
            Id = id,
            DealershipId = allocation.Dealership.Id,
            CustomerId = allocation.Customer.Id,
            VehicleId = allocation.Vehicle.Id,
            ServiceTypeId = allocation.ServiceType.Id,
            ServiceBayId = allocation.ServiceBay.Id,
            TechnicianId = allocation.Technician.Id,
            StartUtc = allocation.Slot.StartUtc,
            EndUtc = allocation.Slot.EndUtc,
            Status = AppointmentStatus.Confirmed,
            Notes = notes,
            CreatedAtUtc = createdAtUtc,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            Version = 1,
        };

        foreach (var slot in allocation.Slot.SlotStarts())
        {
            appointment._reservations.Add(new SlotReservation(id, ResourceKind.ServiceBay, allocation.ServiceBay.Id, slot));
            appointment._reservations.Add(new SlotReservation(id, ResourceKind.Technician, allocation.Technician.Id, slot));
            appointment._reservations.Add(new SlotReservation(id, ResourceKind.Vehicle, allocation.Vehicle.Id, slot));
        }

        return appointment;
    }

    /// <summary>Cancels the appointment and releases its slots. Cancelling twice is a no-op.</summary>
    public Error? Cancel(DateTimeOffset nowUtc)
    {
        if (Status == AppointmentStatus.Cancelled)
        {
            return null;
        }

        if (StartUtc <= nowUtc)
        {
            return AppointmentErrors.AlreadyStarted;
        }

        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        Version++;
        _reservations.Clear();
        return null;
    }

    // The application layer validates these first and returns friendly errors; reaching a throw here means a bug.
    private static void EnsureAllocationIsValid(AppointmentAllocation allocation, DateTimeOffset createdAtUtc)
    {
        var (dealership, customer, vehicle, serviceType, bay, technician, slot) = allocation;
        Require(vehicle.CustomerId == customer.Id, "The vehicle must belong to the customer.");
        Require(bay.DealershipId == dealership.Id && bay.IsActive, "The service bay must be active at the dealership.");
        Require(technician.DealershipId == dealership.Id && technician.IsActive, "The technician must be active at the dealership.");
        Require(technician.IsQualifiedFor(serviceType.Id), "The technician must be qualified for the service type.");
        Require(slot.EndUtc - slot.StartUtc == serviceType.Duration, "The slot must match the service duration.");
        Require(dealership.IsOpenThroughout(slot), "The slot must fall within the dealership's opening hours.");
        Require(slot.StartUtc > createdAtUtc, "An appointment must start in the future.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

public static class AppointmentErrors
{
    public static readonly Error AlreadyStarted = new(
        ErrorKind.BusinessRule,
        "appointment_already_started",
        "The appointment has already started, so it can no longer be cancelled.");
}
