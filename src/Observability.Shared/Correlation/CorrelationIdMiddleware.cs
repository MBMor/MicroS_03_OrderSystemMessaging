using Microsoft.AspNetCore.Http;

namespace Observability.Shared.Correlation;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(correlationIdAccessor);

        if (context.Request.Headers.TryGetValue(CorrelationIdConstants.HeaderName, out var headerValues))
        {
            correlationIdAccessor.CorrelationId = headerValues.Count > 0
                ? headerValues[0]
                : null;
        }

        await _next(context);
    }
}