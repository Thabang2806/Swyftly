using System.Diagnostics;

namespace Mabuntle.Api.Observability;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    MabuntleMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
            logger.LogInformation(
                "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            metrics.RecordError();
            logger.LogError(
                exception,
                "HTTP {Method} {Path} failed in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
