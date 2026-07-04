using System.Net;
using System.Net.Http.Json;
using ApiGateway.IntegrationTests.Common;

namespace ApiGateway.IntegrationTests;

public sealed class GatewayRateLimitingTests(ApiGatewayFactory factory)
    : IClassFixture<ApiGatewayFactory>
{
    private readonly ApiGatewayFactory _factory = factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateOrder_WhenOrderCreationLimitIsExceeded_ReturnsTooManyRequests()
    {
        using var client = _factory.CreateCustomerClient();

        var firstResponse = await PostCreateOrderAsync(client);
        var secondResponse = await PostCreateOrderAsync(client);
        var thirdResponse = await PostCreateOrderAsync(client);
        var fourthResponse = await PostCreateOrderAsync(client);
        var fifthResponse = await PostCreateOrderAsync(client);
        var sixthResponse = await PostCreateOrderAsync(client);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, thirdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, fourthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, fifthResponse.StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixthResponse.StatusCode);

        AssertRoutedTo(firstResponse, "orders");
        AssertRoutedTo(secondResponse, "orders");
        AssertRoutedTo(thirdResponse, "orders");
        AssertRoutedTo(fourthResponse, "orders");
        AssertRoutedTo(fifthResponse, "orders");

        AssertNotRoutedToDownstream(sixthResponse);
    }

    private static Task<HttpResponseMessage> PostCreateOrderAsync(HttpClient client)
    {
        return client.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerId = Guid.NewGuid(),
                items = new[]
                {
                    new
                    {
                        productId = Guid.NewGuid(),
                        quantity = 1
                    }
                }
            });
    }

    private static void AssertRoutedTo(HttpResponseMessage response, string expectedService)
    {
        Assert.True(
            response.Headers.TryGetValues("X-Fake-Service", out var values),
            "Response does not contain X-Fake-Service header.");

        Assert.Equal(expectedService, Assert.Single(values));
    }

    private static void AssertNotRoutedToDownstream(HttpResponseMessage response)
    {
        Assert.False(
            response.Headers.Contains("X-Fake-Service"),
            "Response contains X-Fake-Service header, which means the request was proxied to a downstream service.");
    }
}