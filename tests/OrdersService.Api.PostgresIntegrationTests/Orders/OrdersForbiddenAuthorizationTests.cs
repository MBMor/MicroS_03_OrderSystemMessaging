using System.Net;
using OrdersService.Api.PostgresIntegrationTests.Common;

namespace OrdersService.Api.PostgresIntegrationTests.Orders;

public sealed class OrdersForbiddenAuthorizationTests(OrdersApiFactory factory)
    : IClassFixture<OrdersApiFactory>
{
    private readonly OrdersApiFactory _factory = factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrders_WithCustomerRole_ReturnsForbidden()
    {
        using var client = _factory.CreateCustomerClient();

        var response = await client.GetAsync("/api/v1/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}