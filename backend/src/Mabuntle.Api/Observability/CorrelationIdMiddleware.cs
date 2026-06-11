namespace Mabuntle.Api.Observability;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var candidate = context.Request.Headers.TryGetValue(HeaderName, out var header)
            ? header.ToString()
            : null;
        var trimmed = candidate?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? Guid.NewGuid().ToString("N")
            : trimmed;
    }
}
