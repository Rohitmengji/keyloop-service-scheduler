using Microsoft.AspNetCore.Diagnostics;

namespace ServiceScheduler.Api.Http;

/// <summary>Returns a generic 500 problem for unexpected failures; details stay in the logs, never in the response.</summary>
internal sealed partial class UnhandledExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(logger, httpContext.Request.Method, httpContext.Request.Path.Value);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        if (exception is BadHttpRequestException badRequest)
        {
            // Server-detected client errors (e.g. 413 body too large) keep their status instead of becoming 500s.
            LogBadRequest(logger, httpContext.Request.Method, httpContext.Request.Path.Value, badRequest.StatusCode);
            httpContext.Response.StatusCode = badRequest.StatusCode;
            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = { Status = badRequest.StatusCode, Title = "The request could not be processed." },
            });
        }

        LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path.Value);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = StatusCodes.Status500InternalServerError, Title = "An unexpected error occurred." },
        });
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string? path);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "Client aborted {Method} {Path}")]
    private static partial void LogRequestAborted(ILogger logger, string method, string? path);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Rejected {Method} {Path} with {StatusCode}")]
    private static partial void LogBadRequest(ILogger logger, string method, string? path, int statusCode);
}
