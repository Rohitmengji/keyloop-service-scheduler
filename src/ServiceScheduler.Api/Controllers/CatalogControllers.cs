using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ServiceScheduler.Api.Http;
using ServiceScheduler.Application.Availability;
using ServiceScheduler.Application.Catalog;

namespace ServiceScheduler.Api.Controllers;

/// <summary>Dealerships, their resources and bookable times.</summary>
[Route("api/v1/dealerships")]
public sealed class DealershipsController(CatalogQueries catalog, GetAvailabilityHandler availability)
    : SchedulerControllerBase
{
    /// <summary>Lists dealerships with their active service bays and technicians.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DealershipDto>>(StatusCodes.Status200OK, Json)]
    public async Task<ActionResult<IReadOnlyList<DealershipDto>>> List(CancellationToken cancellationToken) =>
        Ok(await catalog.ListDealershipsAsync(cancellationToken));

    /// <summary>Lists start times on a local date when a bay and a qualified technician are both free for the whole service.</summary>
    /// <param name="dealershipId">The dealership.</param>
    /// <param name="serviceTypeId">The service to fit, which sets the duration.</param>
    /// <param name="date">Local date at the dealership (yyyy-MM-dd). Defaults to the next local day.</param>
    /// <param name="cancellationToken">Aborts the work if the client disconnects.</param>
    /// <response code="200">Available start times, in the dealership's local offset.</response>
    /// <response code="400">serviceTypeId is missing or a parameter is malformed.</response>
    /// <response code="404">No dealership has this id.</response>
    /// <response code="422">The service type does not exist.</response>
    [HttpGet("{dealershipId:guid}/availability")]
    [ProducesResponseType<AvailabilityDto>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public async Task<ActionResult<AvailabilityDto>> GetAvailability(
        Guid dealershipId,
        [FromQuery, BindRequired] Guid serviceTypeId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await availability.HandleAsync(
            new GetAvailabilityQuery(dealershipId, serviceTypeId, date), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ProblemFor(result.Error);
    }
}

/// <summary>The catalogue of bookable services.</summary>
[Route("api/v1/service-types")]
public sealed class ServiceTypesController(CatalogQueries catalog) : SchedulerControllerBase
{
    /// <summary>Lists service types with their durations.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceTypeDto>>(StatusCodes.Status200OK, Json)]
    public async Task<ActionResult<IReadOnlyList<ServiceTypeDto>>> List(CancellationToken cancellationToken) =>
        Ok(await catalog.ListServiceTypesAsync(cancellationToken));
}
