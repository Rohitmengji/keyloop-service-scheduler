using System.Globalization;
using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application;

/// <summary>
/// Every expected failure the API can return. Unknown ids in a request body are 422 (the request is
/// understood but cannot be applied); 404 is reserved for the resource named in the URL.
/// </summary>
public static class SchedulingErrors
{
    public static readonly Error InvalidIdempotencyKey = new(
        ErrorKind.Validation,
        "invalid_idempotency_key",
        $"The Idempotency-Key header must be 1-{Appointment.IdempotencyKeyMaxLength} visible ASCII characters.");

    public static readonly Error NotesTooLong = new(
        ErrorKind.Validation, "notes_too_long", $"Notes cannot exceed {Appointment.NotesMaxLength} characters.");

    public static readonly Error UnknownDealership = new(
        ErrorKind.BusinessRule, "unknown_dealership", "No dealership exists with the given dealershipId.");

    public static readonly Error UnknownServiceType = new(
        ErrorKind.BusinessRule, "unknown_service_type", "No service type exists with the given serviceTypeId.");

    public static readonly Error UnknownCustomer = new(
        ErrorKind.BusinessRule, "unknown_customer", "No customer exists with the given customerId.");

    public static readonly Error UnknownVehicle = new(
        ErrorKind.BusinessRule, "unknown_vehicle", "No vehicle exists with the given vehicleId.");

    public static readonly Error VehicleNotOwnedByCustomer = new(
        ErrorKind.BusinessRule, "vehicle_not_owned_by_customer", "The vehicle is not registered to this customer.");

    public static readonly Error StartTimeNotAligned = new(
        ErrorKind.BusinessRule,
        "start_time_not_aligned",
        $"Appointments start on {Slots.LengthMinutes}-minute boundaries with zero seconds (for example 09:00 or 09:15).");

    public static readonly Error StartTimeInPast = new(
        ErrorKind.BusinessRule, "start_time_in_past", "The requested start time has already passed.");

    public static readonly Error NoActiveServiceBays = new(
        ErrorKind.BusinessRule, "no_service_bays", "The dealership has no active service bays.");

    public static readonly Error IdempotencyKeyReused = new(
        ErrorKind.BusinessRule,
        "idempotency_key_reused",
        "This Idempotency-Key was already used for a different booking request.");

    public static readonly Error VehicleUnavailable = new(
        ErrorKind.Conflict,
        "vehicle_unavailable",
        "The vehicle already has an appointment that overlaps the requested time.");

    public static readonly Error ServiceBayUnavailable = new(
        ErrorKind.Conflict,
        "service_bay_unavailable",
        "No service bay is free for the whole service duration at the requested time.");

    public static readonly Error TechnicianUnavailable = new(
        ErrorKind.Conflict,
        "technician_unavailable",
        "No qualified technician is free for the whole service duration at the requested time.");

    public static readonly Error AllocationContention = new(
        ErrorKind.Conflict,
        "allocation_contention",
        "Other requests are booking this time right now. Please retry.");

    public static readonly Error DealershipNotFound = new(
        ErrorKind.NotFound, "dealership_not_found", "No dealership exists with this id.");

    public static readonly Error AppointmentNotFound = new(
        ErrorKind.NotFound, "appointment_not_found", "No appointment exists with this id.");

    public static Error BeyondBookingHorizon(int days) => new(
        ErrorKind.BusinessRule,
        "beyond_booking_horizon",
        string.Create(CultureInfo.InvariantCulture, $"Appointments can start at most {days} days from now."));

    public static Error OutsideOpeningHours(Dealership dealership) => new(
        ErrorKind.BusinessRule,
        "outside_opening_hours",
        string.Create(
            CultureInfo.InvariantCulture,
            $"The service must start and finish within opening hours, {dealership.OpensAt:HH':'mm}-{dealership.ClosesAt:HH':'mm} ({dealership.TimeZoneId})."));

    public static Error NoQualifiedTechnician(string serviceTypeName) => new(
        ErrorKind.BusinessRule,
        "no_qualified_technician",
        $"No technician at this dealership is qualified for '{serviceTypeName}'.");
}
