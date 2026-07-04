using System.Net;
using System.Net.Http.Json;
using ApiGateway.IntegrationTests.Common;

namespace ApiGateway.IntegrationTests;

public sealed class GatewayAuthorizedRoutingTests(ApiGatewayFactory factory)
    : IClassFixture<ApiGatewayFactory>
{
    private readonly ApiGatewayFactory _factory = factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrders_WithSupportRole_RoutesToOrdersService()
    {
        using var client = _factory.CreateSupportClient();

        var response = await client.GetAsync("/api/v1/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRoutedTo(response, "orders");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrderById_WithCustomerRole_RoutesToOrdersService()
    {
        using var client = _factory.CreateCustomerClient();

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRoutedTo(response, "orders");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateOrder_WithCustomerRole_RoutesToOrdersService()
    {
        using var client = _factory.CreateCustomerClient();

        var response = await client.PostAsJsonAsync(
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertRoutedTo(response, "orders");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInventoryItems_WithSupportRole_RoutesToInventoryService()
    {
        using var client = _factory.CreateSupportClient();

        var response = await client.GetAsync("/api/v1/inventory-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRoutedTo(response, "inventory");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInventoryItemByProductId_WithSupportRole_RoutesToInventoryService()
    {
        using var client = _factory.CreateSupportClient();

        var response = await client.GetAsync($"/api/v1/inventory-items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRoutedTo(response, "inventory");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateInventoryItem_WithAdminRole_RoutesToInventoryService()
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory-items",
            new
            {
                productId = Guid.NewGuid(),
                productName = "Keyboard",
                availableQuantity = 10
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertRoutedTo(response, "inventory");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateInventoryItem_WithAdminRole_RoutesToInventoryService()
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/inventory-items/{Guid.NewGuid()}",
            new
            {
                productName = "Keyboard",
                availableQuantity = 20
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertRoutedTo(response, "inventory");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNotifications_WithSupportRole_RoutesToNotificationsService()
    {
        using var client = _factory.CreateSupportClient();

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRoutedTo(response, "notifications");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNotificationById_WithSupportRole_RoutesToNotificationsService()
    {
        using var client = _factory.CreateSupportClient();

        var response = await client.GetAsync($"/api/v1/notifications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRoutedTo(response, "notifications");
    }

    private static void AssertRoutedTo(HttpResponseMessage response, string expectedService)
    {
        Assert.True(
            response.Headers.TryGetValues("X-Fake-Service", out var values),
            "Response does not contain X-Fake-Service header.");

        Assert.Equal(expectedService, Assert.Single(values));
    }
}