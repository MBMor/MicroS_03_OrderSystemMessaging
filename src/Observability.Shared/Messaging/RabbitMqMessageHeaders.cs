using System.Text;
using System.Text.Json;
using Observability.Shared.Correlation;

namespace Observability.Shared.Messaging;

public static class RabbitMqMessageHeaders
{
    public const string CorrelationIdHeaderName = CorrelationIdConstants.HeaderName;

    public static void SetCorrelationId(
        IDictionary<string, object?> headers,
        string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var normalizedCorrelationId = CorrelationIdValidator.Normalize(correlationId);

        if (normalizedCorrelationId is null)
        {
            return;
        }

        headers[CorrelationIdHeaderName] = Encoding.UTF8.GetBytes(normalizedCorrelationId);
    }

    public static string? GetCorrelationId(
        IDictionary<string, object?>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return null;
        }

        if (!TryGetHeaderValue(
                headers,
                CorrelationIdHeaderName,
                out var headerValue))
        {
            return null;
        }

        var correlationId = ConvertHeaderValueToString(headerValue);

        return CorrelationIdValidator.Normalize(correlationId);
    }

    public static string GetCorrelationIdOrCreate(
        IDictionary<string, object?>? headers)
    {
        return GetCorrelationId(headers)
            ?? CorrelationIdGenerator.Create();
    }

    public static string ResolveCorrelationId(
    IDictionary<string, object?>? headers,
    string? payloadCorrelationId)
    {
        return GetCorrelationId(headers)
            ?? CorrelationIdValidator.Normalize(payloadCorrelationId)
            ?? CorrelationIdGenerator.Create();
    }

    public static string? GetCorrelationIdFromJsonPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (TryGetStringProperty(
                    document.RootElement,
                    "correlationId",
                    out var camelCaseCorrelationId))
            {
                return CorrelationIdValidator.Normalize(camelCaseCorrelationId);
            }

            if (TryGetStringProperty(
                    document.RootElement,
                    "CorrelationId",
                    out var pascalCaseCorrelationId))
            {
                return CorrelationIdValidator.Normalize(pascalCaseCorrelationId);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetHeaderValue(
        IDictionary<string, object?> headers,
        string headerName,
        out object? headerValue)
    {
        if (headers.TryGetValue(headerName, out headerValue))
        {
            return true;
        }

        foreach (var header in headers)
        {
            if (string.Equals(
                    header.Key,
                    headerName,
                    StringComparison.OrdinalIgnoreCase))
            {
                headerValue = header.Value;
                return true;
            }
        }

        headerValue = null;
        return false;
    }

    private static string? ConvertHeaderValueToString(object? headerValue)
    {
        return headerValue switch
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

            _ => headerValue.ToString()
        };
    }

    private static bool TryGetStringProperty(
        JsonElement rootElement,
        string propertyName,
        out string? value)
    {
        value = null;

        if (rootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!rootElement.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }
}