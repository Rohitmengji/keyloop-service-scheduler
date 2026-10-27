using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Common;

namespace ServiceScheduler.Application.Appointments;

/// <summary>Cancels an appointment and releases its slots. Idempotent: cancelling twice returns the same result.</summary>
public sealed partial class CancelAppointmentHandler(
    ISchedulerDbContext db,
    AppointmentQueries appointments,
    TimeProvider clock,
    SchedulerTelemetry telemetry,
    ILogger<CancelAppointmentHandler> logger)
{
    public async Task<Result<AppointmentDto>> HandleAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        using var activity = SchedulerTelemetry.ActivitySource.StartActivity("CancelAppointment");
        activity?.SetTag("scheduler.appointment_id", appointmentId);

        for (var attempt = 1; ; attempt++)
        {
            var appointment = await db.Appointments
                .Include(a => a.Reservations)
                .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);
            if (appointment is null)
            {
                return SchedulingErrors.AppointmentNotFound;
            }

            var wasConfirmed = appointment.Status == AppointmentStatus.Confirmed;
            if (appointment.Cancel(clock.GetUtcNow()) is { } error)
            {
                return error;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                // A concurrent request changed this appointment first; re-apply the cancellation to the latest state.
                db.ChangeTracker.Clear();
                continue;
            }

            if (wasConfirmed)
            {
                telemetry.AppointmentCancelled();
                LogAppointmentCancelled(logger, appointmentId);
            }

            var cancelled = await appointments.GetByIdAsync(appointmentId, cancellationToken)
                ?? throw new InvalidOperationException($"Appointment {appointmentId} disappeared after cancellation.");
            return cancelled;
        }
    }

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Appointment {AppointmentId} cancelled")]
    private static partial void LogAppointmentCancelled(ILogger logger, Guid appointmentId);
}
