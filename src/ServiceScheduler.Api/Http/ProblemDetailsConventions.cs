using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServiceScheduler.Domain.Common;

namespace ServiceScheduler.Api.Http;

internal static class ProblemDetailsConventions
{
    /// <summary>Gives every problem response a trace id and a machine-readable code, including framework-generated ones.</summary>
    public static void Apply(ProblemDetailsContext context)
    {
        var extensions = context.ProblemDetails.Extensions;
        extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        extensions.TryAdd("code", context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "validation_failed",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
            StatusCodes.Status413PayloadTooLarge => "payload_too_large",
            StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
            >= StatusCodes.Status500InternalServerError => "internal_error",
            _ => "request_failed",
        });
    }
}

// No class-level [Produces]: as a result filter it would overwrite application/problem+json on error responses.
[ApiController]
public abstract class SchedulerControllerBase : ControllerBase
{
    protected const string Json = "application/json";
    protected const string ProblemJson = "application/problem+json";

    /// <summary>Maps an application error to its single HTTP status and an RFC 9457 problem response.</summary>
    protected ObjectResult ProblemFor(Error error)
    {
        var (status, title) = error.Kind switch
        {
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "The request is invalid."),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "The resource was not found."),
            ErrorKind.BusinessRule => (StatusCodes.Status422UnprocessableEntity, "The request cannot be processed."),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "The requested time is not available."),
            _ => throw new UnreachableException($"Unmapped error kind {error.Kind}."),
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title, detail: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { ProblemJson } };
    }
}
