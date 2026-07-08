using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Observability.Shared.Correlation;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private static readonly Func<ILogger, string, IDisposable?> CorrelationIdScope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    private readonly RequestDelegate _next = next;
    private readonly ILogger<CorrelationIdMiddleware> _logger = logger;

    public async Task InvokeAsync(
        HttpContext context,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(correlationIdAccessor);

        var incomingCorrelationId = GetSingleHeaderValue(
            context.Request.Headers,
            CorrelationIdConstants.HeaderName);

        var correlationId =
            CorrelationIdValidator.Normalize(incomingCorrelationId)
            ?? CorrelationIdGenerator.Create();

        correlationIdAccessor.CorrelationId = correlationId;

        context.Response.OnStarting(static state =>
        {
            var (httpContext, currentCorrelationId) = ((HttpContext, string))state;

            httpContext.Response.Headers[CorrelationIdConstants.HeaderName] = currentCorrelationId;

            return Task.CompletedTask;
        }, (context, correlationId));

        using var scope = CorrelationIdScope(
            _logger,
            correlationId);

        await _next(context);
    }

    private static string? GetSingleHeaderValue(
        IHeaderDictionary headers,
        string headerName)
    {
        if (!headers.TryGetValue(headerName, out StringValues values))
        {
            return null;
        }

        return values.Count == 1
            ? values[0]
            : null;
    }
}