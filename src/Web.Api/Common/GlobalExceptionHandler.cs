using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Web.Api.Common;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails;

        if (exception is BadHttpRequestException badRequest)
        {
            // The client's fault (unbindable route/query value, malformed body): its own status, not a 500.
            logger.LogWarning(exception, "Bad request: {Reason}", badRequest.Message);
            problemDetails = new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Bad request",
                Detail = badRequest.Message
            };
        }
        else
        {
            logger.LogError(exception, "Unhandled exception occurred");
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                Title = "Server failure"
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        // RFC 7807 media type (WriteAsJsonAsync would default to application/json).
        await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", cancellationToken);

        return true;
    }
}
