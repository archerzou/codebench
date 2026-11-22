using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace Web.Api.Common.Middleware;

public sealed class RequestContextLoggingMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Awaited inside the using: the CorrelationId property must stay pushed for the whole pipeline,
        // not just until its first await.
        using (LogContext.PushProperty("CorrelationId", GetCorrelationId(context)))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        context.Request.Headers.TryGetValue(
            CorrelationIdHeaderName,
            out StringValues correlationId);

        return correlationId.FirstOrDefault() ?? context.TraceIdentifier;
    }
}
