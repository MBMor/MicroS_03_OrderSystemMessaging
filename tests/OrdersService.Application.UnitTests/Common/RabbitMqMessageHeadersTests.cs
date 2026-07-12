using System.Text;
using Observability.Shared.Correlation;
using Observability.Shared.Messaging;

namespace OrdersService.Application.UnitTests.Common;

public sealed class RabbitMqMessageHeadersTests
{
    [Fact]
    public void SetCorrelationId_StoresNormalizedCorrelationId_AsByteArrayHeader()
    {
        const string correlationId = "  test-correlation-069  ";

        var headers = new Dictionary<string, object?>();

        RabbitMqMessageHeaders.SetCorrelationId(
            headers,
            correlationId);

        Assert.True(
            headers.ContainsKey(RabbitMqMessageHeaders.CorrelationIdHeaderName));

        var headerValue = Assert.IsType<byte[]>(
            headers[RabbitMqMessageHeaders.CorrelationIdHeaderName]);

        Assert.Equal(
            "test-correlation-069",
            Encoding.UTF8.GetString(headerValue));
    }

    [Fact]
    public void SetCorrelationId_DoesNotSetHeader_WhenCorrelationIdIsInvalid()
    {
        var headers = new Dictionary<string, object?>();

        RabbitMqMessageHeaders.SetCorrelationId(
            headers,
            correlationId: "   ");

        Assert.False(
            headers.ContainsKey(RabbitMqMessageHeaders.CorrelationIdHeaderName));
    }

    [Fact]
    public void GetCorrelationId_ReadsCorrelationId_FromByteArrayHeader()
    {
        const string correlationId = "test-correlation-byte-array-069";

        var headers = new Dictionary<string, object?>
        {
            [RabbitMqMessageHeaders.CorrelationIdHeaderName] =
                Encoding.UTF8.GetBytes(correlationId)
        };

        var result = RabbitMqMessageHeaders.GetCorrelationId(headers);

        Assert.Equal(
            correlationId,
            result);
    }

    [Fact]
    public void GetCorrelationId_ReadsCorrelationId_FromStringHeader()
    {
        const string correlationId = "test-correlation-string-069";

        var headers = new Dictionary<string, object?>
        {
            [RabbitMqMessageHeaders.CorrelationIdHeaderName] = correlationId
        };

        var result = RabbitMqMessageHeaders.GetCorrelationId(headers);

        Assert.Equal(
            correlationId,
            result);
    }

    [Fact]
    public void GetCorrelationId_ReadsCorrelationId_CaseInsensitively()
    {
        const string correlationId = "test-correlation-case-insensitive-069";

        var headers = new Dictionary<string, object?>
        {
            ["x-correlation-id"] = Encoding.UTF8.GetBytes(correlationId)
        };

        var result = RabbitMqMessageHeaders.GetCorrelationId(headers);

        Assert.Equal(
            correlationId,
            result);
    }

    [Fact]
    public void GetCorrelationId_ReturnsNull_WhenHeadersAreMissing()
    {
        var result = RabbitMqMessageHeaders.GetCorrelationId(
            headers: null);

        Assert.Null(result);
    }

    [Fact]
    public void GetCorrelationIdOrCreate_ReturnsExistingCorrelationId_WhenHeaderExists()
    {
        const string correlationId = "test-correlation-existing-069";

        var headers = new Dictionary<string, object?>
        {
            [RabbitMqMessageHeaders.CorrelationIdHeaderName] =
                Encoding.UTF8.GetBytes(correlationId)
        };

        var result = RabbitMqMessageHeaders.GetCorrelationIdOrCreate(headers);

        Assert.Equal(
            correlationId,
            result);
    }

    [Fact]
    public void GetCorrelationIdOrCreate_CreatesCorrelationId_WhenHeaderIsMissing()
    {
        var result = RabbitMqMessageHeaders.GetCorrelationIdOrCreate(
            headers: null);

        Assert.False(string.IsNullOrWhiteSpace(result));

        Assert.Equal(
            result,
            CorrelationIdValidator.Normalize(result));
    }

    [Fact]
    public void ResolveCorrelationId_PrefersHeaderCorrelationId_OverPayloadCorrelationId()
    {
        const string headerCorrelationId = "test-correlation-header-069";
        const string payloadCorrelationId = "test-correlation-payload-069";

        var headers = new Dictionary<string, object?>
        {
            [RabbitMqMessageHeaders.CorrelationIdHeaderName] =
                Encoding.UTF8.GetBytes(headerCorrelationId)
        };

        var result = RabbitMqMessageHeaders.ResolveCorrelationId(
            headers,
            payloadCorrelationId);

        Assert.Equal(
            headerCorrelationId,
            result);
    }

    [Fact]
    public void ResolveCorrelationId_UsesPayloadCorrelationId_WhenHeaderIsMissing()
    {
        const string payloadCorrelationId = "test-correlation-payload-only-069";

        var result = RabbitMqMessageHeaders.ResolveCorrelationId(
            headers: null,
            payloadCorrelationId);

        Assert.Equal(
            payloadCorrelationId,
            result);
    }

    [Fact]
    public void ResolveCorrelationId_CreatesCorrelationId_WhenHeaderAndPayloadAreMissing()
    {
        var result = RabbitMqMessageHeaders.ResolveCorrelationId(
            headers: null,
            payloadCorrelationId: null);

        Assert.False(string.IsNullOrWhiteSpace(result));

        Assert.Equal(
            result,
            CorrelationIdValidator.Normalize(result));
    }

    [Fact]
    public void GetCorrelationIdFromJsonPayload_ReadsCamelCaseCorrelationId()
    {
        const string payload = """
        {
          "eventId": "11111111-1111-1111-1111-111111111111",
          "eventType": "OrderCreated",
          "correlationId": "test-correlation-camel-069"
        }
        """;

        var result = RabbitMqMessageHeaders.GetCorrelationIdFromJsonPayload(payload);

        Assert.Equal(
            "test-correlation-camel-069",
            result);
    }

    [Fact]
    public void GetCorrelationIdFromJsonPayload_ReadsPascalCaseCorrelationId()
    {
        const string payload = """
        {
          "EventId": "11111111-1111-1111-1111-111111111111",
          "EventType": "OrderCreated",
          "CorrelationId": "test-correlation-pascal-069"
        }
        """;

        var result = RabbitMqMessageHeaders.GetCorrelationIdFromJsonPayload(payload);

        Assert.Equal(
            "test-correlation-pascal-069",
            result);
    }

    [Fact]
    public void GetCorrelationIdFromJsonPayload_ReturnsNull_WhenPayloadDoesNotContainCorrelationId()
    {
        const string payload = """
        {
          "eventId": "11111111-1111-1111-1111-111111111111",
          "eventType": "OrderCreated"
        }
        """;

        var result = RabbitMqMessageHeaders.GetCorrelationIdFromJsonPayload(payload);

        Assert.Null(result);
    }

    [Fact]
    public void GetCorrelationIdFromJsonPayload_ReturnsNull_WhenPayloadIsInvalidJson()
    {
        const string payload = """
        {
          "eventId": "11111111-1111-1111-1111-111111111111",
        """;

        var result = RabbitMqMessageHeaders.GetCorrelationIdFromJsonPayload(payload);

        Assert.Null(result);
    }
}