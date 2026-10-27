using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Appointments;

public sealed record BookingConfirmation(AppointmentDto Appointment, bool IsReplay);

/// <summary>
/// Books an appointment: validates the request, picks a free qualified technician and a free bay for the
/// whole duration, and persists the appointment with its slot reservations in one transaction.
/// The database primary key on reservations is the final arbiter between concurrent requests; losing
/// that race triggers a fresh read and another allocation attempt.
/// </summary>
public sealed partial class BookAppointmentHandler(
    ISchedulerDbContext db,
    IConstraintViolationDetector constraintViolations,
    AppointmentQueries appointments,
    TimeProvider clock,
    IOptions<SchedulingOptions> options,
    SchedulerTelemetry telemetry,
    ILogger<BookAppointmentHandler> logger)
{
    public async Task<Result<BookingConfirmation>> HandleAsync(
        BookAppointmentCommand command, CancellationToken cancellationToken)
    {
        using var activity = SchedulerTelemetry.ActivitySource.StartActivity("BookAppointment");
        activity?.SetTag("scheduler.dealership_id", command.DealershipId);
        activity?.SetTag("scheduler.service_type_id", command.ServiceTypeId);

        var result = await BookAsync(Normalise(command), cancellationToken);

        if (!result.IsSuccess)
        {
            activity?.SetTag("scheduler.outcome", result.Error.Code);
            telemetry.BookingRejected(result.Error.Code);
            LogBookingRejected(logger, command.DealershipId, result.Error.Code);
        }
        else if (result.Value.IsReplay)
        {
            activity?.SetTag("scheduler.outcome", "replayed");
            telemetry.IdempotentReplay();
        }
        else
        {
            activity?.SetTag("scheduler.outcome", "booked");
            telemetry.AppointmentBooked(result.Value.Appointment.ServiceType.Code);
        }

        return result;
    }

    private async Task<Result<BookingConfirmation>> BookAsync(BookAppointmentCommand command, CancellationToken ct)
    {
        if (command.IdempotencyKey is { } key && !IsValidIdempotencyKey(key))
        {
            return SchedulingErrors.InvalidIdempotencyKey;
        }

        if (command.Notes?.Length > Appointment.NotesMaxLength)
        {
            return SchedulingErrors.NotesTooLong;
        }

        var fingerprint = command.IdempotencyKey is null ? null : RequestFingerprint.Compute(command);
        if (await FindReplayAsync(command.IdempotencyKey, fingerprint, ct) is { } replay)
        {
            return replay;
        }

        if (!Slots.IsAligned(command.StartTime))
        {
            return SchedulingErrors.StartTimeNotAligned;
        }

        var dealership = await db.Dealerships.AsNoTracking().FirstOrDefaultAsync(d => d.Id == command.DealershipId, ct);
        if (dealership is null)
        {
            return SchedulingErrors.UnknownDealership;
        }

        var serviceType = await db.ServiceTypes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == command.ServiceTypeId, ct);
        if (serviceType is null)
        {
            return SchedulingErrors.UnknownServiceType;
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == command.CustomerId, ct);
        if (customer is null)
        {
            return SchedulingErrors.UnknownCustomer;
        }

        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == command.VehicleId, ct);
        if (vehicle is null)
        {
            return SchedulingErrors.UnknownVehicle;
        }

        if (vehicle.CustomerId != customer.Id)
        {
            return SchedulingErrors.VehicleNotOwnedByCustomer;
        }

        var now = clock.GetUtcNow();
        // Instants are stored with millisecond precision; truncating here keeps the 201 body identical to later reads.
        now = DateTimeOffset.FromUnixTimeMilliseconds(now.ToUnixTimeMilliseconds());
        var range = TimeRange.Create(command.StartTime, serviceType.Duration);
        if (range.StartUtc <= now)
        {
            return SchedulingErrors.StartTimeInPast;
        }

        var horizonDays = options.Value.BookingHorizonDays;
        if (range.StartUtc > now.AddDays(horizonDays))
        {
            return SchedulingErrors.BeyondBookingHorizon(horizonDays);
        }

        if (!dealership.IsOpenThroughout(range))
        {
            return SchedulingErrors.OutsideOpeningHours(dealership);
        }

        var bays = await db.ServiceBays.AsNoTracking()
            .Where(b => b.DealershipId == dealership.Id && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
        if (bays.Count == 0)
        {
            return SchedulingErrors.NoActiveServiceBays;
        }

        var technicians = await db.Technicians.AsNoTracking()
            .Include(t => t.Qualifications)
            .Where(t => t.DealershipId == dealership.Id
                && t.IsActive
                && t.Qualifications.Any(q => q.ServiceTypeId == serviceType.Id))
            .OrderBy(t => t.FullName)
            .ToListAsync(ct);
        if (technicians.Count == 0)
        {
            return SchedulingErrors.NoQualifiedTechnician(serviceType.Name);
        }

        List<Guid> resourceIds = [.. bays.Select(b => b.Id), .. technicians.Select(t => t.Id), vehicle.Id];

        for (var attempt = 1; ; attempt++)
        {
            var occupancy = await db.LoadOccupancyAsync(resourceIds, range.StartUtc, range.EndUtc, ct);

            if (!occupancy.IsFree(ResourceKind.Vehicle, vehicle.Id, range))
            {
                return await ReplayOrAsync(SchedulingErrors.VehicleUnavailable);
            }

            var bay = bays.FirstOrDefault(b => occupancy.IsFree(ResourceKind.ServiceBay, b.Id, range));
            if (bay is null)
            {
                return await ReplayOrAsync(SchedulingErrors.ServiceBayUnavailable);
            }

            var technician = technicians.FirstOrDefault(t => occupancy.IsFree(ResourceKind.Technician, t.Id, range));
            if (technician is null)
            {
                return await ReplayOrAsync(SchedulingErrors.TechnicianUnavailable);
            }

            var appointment = Appointment.Confirm(
                Guid.CreateVersion7(now),
                new AppointmentAllocation(dealership, customer, vehicle, serviceType, bay, technician, range),
                now,
                command.Notes,
                command.IdempotencyKey,
                fingerprint);

            db.Appointments.Add(appointment);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (constraintViolations.IsUniqueViolation(exception))
            {
                db.ChangeTracker.Clear();
                if (await FindReplayAsync(command.IdempotencyKey, fingerprint, ct) is { } concurrentReplay)
                {
                    return concurrentReplay;
                }

                if (attempt >= options.Value.MaxAllocationAttempts)
                {
                    return SchedulingErrors.AllocationContention;
                }

                telemetry.AllocationConflict();
                LogAllocationConflict(logger, dealership.Id, attempt);
                continue;
            }

            Activity.Current?.SetTag("scheduler.allocation_attempts", attempt);
            LogAppointmentBooked(logger, appointment.Id, dealership.Id, bay.Id, technician.Id, appointment.StartUtc);
            var booked = AppointmentDto.From(appointment, dealership, customer, vehicle, serviceType, bay, technician);
            return new BookingConfirmation(booked, IsReplay: false);
        }

        // A request with the same idempotency key may have taken these slots after our first replay check.
        async Task<Result<BookingConfirmation>> ReplayOrAsync(Error conflict) =>
            await FindReplayAsync(command.IdempotencyKey, fingerprint, ct) ?? conflict;
    }

    private async Task<Result<BookingConfirmation>?> FindReplayAsync(
        string? idempotencyKey, string? fingerprint, CancellationToken ct)
    {
        if (idempotencyKey is null)
        {
            return null;
        }

        var original = await db.Appointments.AsNoTracking()
            .Where(a => a.IdempotencyKey == idempotencyKey)
            .Select(a => new { a.Id, a.RequestFingerprint })
            .FirstOrDefaultAsync(ct);

        if (original is null)
        {
            return null;
        }

        if (original.RequestFingerprint != fingerprint)
        {
            return SchedulingErrors.IdempotencyKeyReused;
        }

        var appointment = await appointments.GetByIdAsync(original.Id, ct)
            ?? throw new InvalidOperationException($"Appointment {original.Id} disappeared during an idempotent replay.");
        return new BookingConfirmation(appointment, IsReplay: true);
    }

    private static BookAppointmentCommand Normalise(BookAppointmentCommand command) =>
        command with { Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim() };

    private static bool IsValidIdempotencyKey(string key) =>
        key.Length is > 0 and <= Appointment.IdempotencyKeyMaxLength && key.All(c => c is > ' ' and <= '~');

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Appointment {AppointmentId} booked at dealership {DealershipId} in bay {ServiceBayId} with technician {TechnicianId}, starting {StartUtc}")]
    private static partial void LogAppointmentBooked(
        ILogger logger, Guid appointmentId, Guid dealershipId, Guid serviceBayId, Guid technicianId, DateTimeOffset startUtc);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Booking rejected at dealership {DealershipId} with {ErrorCode}")]
    private static partial void LogBookingRejected(ILogger logger, Guid dealershipId, string errorCode);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "Allocation attempt {Attempt} at dealership {DealershipId} lost a race to a concurrent booking; retrying")]
    private static partial void LogAllocationConflict(ILogger logger, Guid dealershipId, int attempt);
}
