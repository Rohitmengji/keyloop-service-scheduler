using Microsoft.AspNetCore.Mvc;
using ServiceScheduler.Api.Contracts;
using ServiceScheduler.Api.Http;
using ServiceScheduler.Application;
using ServiceScheduler.Application.Appointments;

namespace ServiceScheduler.Api.Controllers;

/// <summary>Book, read and cancel service appointments.</summary>
[Route("api/v1/appointments")]
public sealed class AppointmentsController(
    BookAppointmentHandler bookAppointment,
    CancelAppointmentHandler cancelAppointment,
    AppointmentQueries appointments) : SchedulerControllerBase
{
    private const int MaxRequestBytes = 16 * 1024;

    /// <summary>Books an appointment, allocating a bay and a qualified technician that are free for the whole service.</summary>
    /// <param name="request">The vehicle, service, dealership and requested start time.</param>
    /// <param name="idempotencyKey">
    /// Optional client-generated key (for example a UUID). Retrying with the same key and body returns the
    /// original appointment with an <c>Idempotent-Replayed: true</c> header instead of booking twice.
    /// </param>
    /// <param name="cancellationToken">Aborts the work if the client disconnects.</param>
    /// <response code="201">The appointment is confirmed.</response>
    /// <response code="400">The request is malformed (missing fields, timestamp without offset, invalid key).</response>
    /// <response code="409">No bay or qualified technician is free, or the vehicle is already booked, at that time.</response>
    /// <response code="422">The request breaks a business rule (unknown ids, outside opening hours, past, unqualified).</response>
    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public async Task<IActionResult> Book(
        BookAppointmentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await bookAppointment.HandleAsync(request.ToCommand(idempotencyKey), cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFor(result.Error);
        }

        var (appointment, isReplay) = result.Value;
        if (isReplay)
        {
            Response.Headers["Idempotent-Replayed"] = "true";
        }

        return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
    }

    /// <summary>Returns an appointment with its customer, vehicle, bay and technician.</summary>
    /// <response code="200">The appointment.</response>
    /// <response code="404">No appointment has this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<AppointmentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await appointments.GetByIdAsync(id, cancellationToken);
        return appointment is null ? ProblemFor(SchedulingErrors.AppointmentNotFound) : Ok(appointment);
    }

    /// <summary>Cancels an appointment and releases its bay, technician and vehicle slots. Safe to retry.</summary>
    /// <response code="200">The appointment is cancelled (or already was).</response>
    /// <response code="404">No appointment has this id.</response>
    /// <response code="422">The appointment has already started.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public async Task<ActionResult<AppointmentDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await cancelAppointment.HandleAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ProblemFor(result.Error);
    }
}
