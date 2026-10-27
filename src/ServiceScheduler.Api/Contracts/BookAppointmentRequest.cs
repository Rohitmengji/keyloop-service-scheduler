using System.ComponentModel.DataAnnotations;
using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Appointments;

namespace ServiceScheduler.Api.Contracts;

/// <summary>A request to book a service appointment.</summary>
public sealed record BookAppointmentRequest
{
    /// <summary>The dealership where the service will take place.</summary>
    /// <example>de000000-0000-0000-0000-000000000001</example>
    [Required]
    public Guid? DealershipId { get; init; }

    /// <summary>The customer making the booking; must be the vehicle's registered keeper.</summary>
    /// <example>c0000000-0000-0000-0000-000000000001</example>
    [Required]
    public Guid? CustomerId { get; init; }

    /// <summary>The vehicle to be serviced.</summary>
    /// <example>ca000000-0000-0000-0000-000000000001</example>
    [Required]
    public Guid? VehicleId { get; init; }

    /// <summary>The service to perform; its duration decides how long the bay and technician are reserved.</summary>
    /// <example>5e000000-0000-0000-0000-000000000001</example>
    [Required]
    public Guid? ServiceTypeId { get; init; }

    /// <summary>
    /// Requested start as ISO 8601 with an explicit UTC offset, on a 15-minute boundary
    /// (for example 2026-11-02T09:00:00+00:00). Use the availability endpoint to find free times.
    /// </summary>
    [Required]
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Optional note for the workshop.</summary>
    /// <example>Customer will wait on site</example>
    [MaxLength(Appointment.NotesMaxLength)]
    public string? Notes { get; init; }

    public BookAppointmentCommand ToCommand(string? idempotencyKey) => new(
        DealershipId.GetValueOrDefault(),
        CustomerId.GetValueOrDefault(),
        VehicleId.GetValueOrDefault(),
        ServiceTypeId.GetValueOrDefault(),
        StartTime.GetValueOrDefault(),
        Notes,
        idempotencyKey);
}
