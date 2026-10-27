using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ServiceScheduler.Application;

/// <summary>Business metrics and trace spans. Tags carry codes and ids only, never customer details.</summary>
public sealed class SchedulerTelemetry
{
    public const string SourceName = "ServiceScheduler";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    private readonly Counter<long> _booked;
    private readonly Counter<long> _rejected;
    private readonly Counter<long> _allocationConflicts;
    private readonly Counter<long> _idempotentReplays;
    private readonly Counter<long> _cancelled;

    public SchedulerTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(SourceName);
        _booked = meter.CreateCounter<long>(
            "scheduler.appointments.booked", "{appointment}", "Appointments confirmed.");
        _rejected = meter.CreateCounter<long>(
            "scheduler.bookings.rejected", "{request}", "Booking requests rejected, tagged with the error code.");
        _allocationConflicts = meter.CreateCounter<long>(
            "scheduler.bookings.allocation_conflicts", "{attempt}", "Allocations lost to a concurrent booking and retried.");
        _idempotentReplays = meter.CreateCounter<long>(
            "scheduler.bookings.idempotent_replays", "{request}", "Retried requests answered with the original appointment.");
        _cancelled = meter.CreateCounter<long>(
            "scheduler.appointments.cancelled", "{appointment}", "Appointments cancelled.");
    }

    public void AppointmentBooked(string serviceTypeCode) =>
        _booked.Add(1, new KeyValuePair<string, object?>("service_type", serviceTypeCode));

    public void BookingRejected(string errorCode) =>
        _rejected.Add(1, new KeyValuePair<string, object?>("reason", errorCode));

    public void AllocationConflict() => _allocationConflicts.Add(1);

    public void IdempotentReplay() => _idempotentReplays.Add(1);

    public void AppointmentCancelled() => _cancelled.Add(1);
}
