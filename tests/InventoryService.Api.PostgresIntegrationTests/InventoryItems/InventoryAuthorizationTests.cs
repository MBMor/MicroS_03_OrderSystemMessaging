using System.Net;
using System.Net.Http.Json;
using InventoryService.Api.PostgresIntegrationTests.Common;

namespace InventoryService.Api.PostgresIntegrationTests.InventoryItems;

public sealed class InventoryAuthorizationTests(InventoryApiFactory factory)
    : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory = factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateInventoryItem_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory-items",
            new
            {
                productId = Guid.NewGuid(),
                productName = "Keyboard",
                availableQuantity = 10
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInventoryItems_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.GetAsync("/api/v1/inventory-items");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInventoryItemByProductId_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.GetAsync($"/api/v1/inventory-items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateInventoryItem_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/inventory-items/{Guid.NewGuid()}",
            new
            {
                productName = "Keyboard",
                availableQuantity = 10
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}