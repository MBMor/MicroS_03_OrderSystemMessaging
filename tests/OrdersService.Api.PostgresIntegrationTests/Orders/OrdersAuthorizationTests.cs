using System.Net;
using System.Net.Http.Json;
using OrdersService.Api.PostgresIntegrationTests.Common;

namespace OrdersService.Api.PostgresIntegrationTests.Orders;

public sealed class OrdersAuthorizationTests(OrdersApiFactory factory)
    : IClassFixture<OrdersApiFactory>
{
    private readonly OrdersApiFactory _factory = factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateOrder_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerName = "John Doe",
                customerEmail = "john.doe@example.com",
                items = new[]
                {
                    new
                    {
                        productId = Guid.NewGuid(),
                        productName = "Keyboard",
                        quantity = 1
                    }
                }
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrders_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.GetAsync("/api/v1/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrderById_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}