using System.Diagnostics;
using System.Text;
using Observability.Shared.Tracing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Observability.Shared.Messaging;

public static class RabbitMqTraceContextHeaders
{
    public const string TraceParentHeaderName = "traceparent";
    public const string TraceStateHeaderName = "tracestate";

    private static readonly TextMapPropagator Propagator =
        new CompositeTextMapPropagator(
            [
                new TraceContextPropagator(),
                new BaggagePropagator()
            ]);

    public static TraceContextSnapshot CaptureCurrent()
    {
        if (Activity.Current is null)
        {
            return TraceContextSnapshot.Empty;
        }

        var carrier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Propagator.Inject(
            new PropagationContext(
                Activity.Current.Context,
                Baggage.Current),
            carrier,
            static (headers, name, value) =>
            {
                headers[name] = value;
            });

        carrier.TryGetValue(
            TraceParentHeaderName,
            out var traceParent);

        carrier.TryGetValue(
            TraceStateHeaderName,
            out var traceState);

        return new TraceContextSnapshot(
            Normalize(traceParent),
            Normalize(traceState));
    }

    public static void InjectCurrent(
        IDictionary<string, object?> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (Activity.Current is null)
        {
            return;
        }

        Propagator.Inject(
            new PropagationContext(
                Activity.Current.Context,
                Baggage.Current),
            headers,
            static (carrier, name, value) =>
            {
                carrier[name] = Encoding.UTF8.GetBytes(value);
            });
    }

    public static void SetTraceContext(
        IDictionary<string, object?> headers,
        string? traceParent,
        string? traceState)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var normalizedTraceParent = Normalize(traceParent);

        if (normalizedTraceParent is not null)
        {
            headers[TraceParentHeaderName] = Encoding.UTF8.GetBytes(normalizedTraceParent);
        }

        var normalizedTraceState = Normalize(traceState);

        if (normalizedTraceState is not null)
        {
            headers[TraceStateHeaderName] = Encoding.UTF8.GetBytes(normalizedTraceState);
        }
    }

    public static PropagationContext Extract(
        IDictionary<string, object?>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return default;
        }

        return Propagator.Extract(
            default,
            headers,
            static (carrier, name) =>
            {
                var value = GetHeaderValue(
                    carrier,
                    name);

                return value is null
                    ? []
                    : [value];
            });
    }

    public static PropagationContext Extract(
        string? traceParent,
        string? traceState)
    {
        var carrier = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        SetTraceContext(
            carrier,
            traceParent,
            traceState);

        return Extract(carrier);
    }

    private static string? GetHeaderValue(
        IDictionary<string, object?> headers,
        string name)
    {
        if (headers.TryGetValue(name, out var value))
        {
            return ConvertHeaderValueToString(value);
        }

        foreach (var header in headers)
        {
            if (string.Equals(
                    header.Key,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ConvertHeaderValueToString(header.Value);
            }
        }

        return null;
    }

    private static string? ConvertHeaderValueToString(object? value)
    {
        return value switch
        {
            null => null,
            string stringValue => stringValue,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> readOnlyMemory => Encoding.UTF8.GetString(readOnlyMemory.Span),
            Memory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            ArraySegment<byte> segment when segment.Array is not null =>
                Encoding.UTF8.GetString(
                    segment.Array,
                    segment.Offset,
                    segment.Count),
            _ => value.ToString()
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}