using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryService.Api.PostgresIntegrationTests.Common;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryService.Api.PostgresIntegrationTests.InventoryItems;

public sealed class InventoryItemsApiTests(InventoryApiFactory factory) 
    : IClassFixture<InventoryApiFactory>, IAsyncLifetime
{
    private readonly InventoryApiFactory _factory = factory;
    private readonly HttpClient _client = factory.HttpClient;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateInventoryItem_WithValidRequest_ReturnsCreatedAndPersistsItem()
    {
        var productId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/inventory-items",
            new
            {
                productId,
                productName = "Keyboard",
                availableQuantity = 50
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(productId, document.RootElement.GetProperty("productId").GetGuid());
        Assert.Equal("Keyboard", document.RootElement.GetProperty("productName").GetString());
        Assert.Equal(50, document.RootElement.GetProperty("availableQuantity").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("reservedQuantity").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var item = await dbContext.InventoryItems.SingleAsync();

        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Keyboard", item.ProductName);
        Assert.Equal(50, item.AvailableQuantity);
        Assert.Equal(0, item.ReservedQuantity);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateInventoryItem_WhenProductAlreadyExists_ReturnsConflict()
    {
        var productId = Guid.NewGuid();

        var firstResponse = await CreateInventoryItemAsync(productId);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await CreateInventoryItemAsync(productId);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInventoryItem_WhenItemExists_ReturnsItem()
    {
        var productId = Guid.NewGuid();

        var createResponse = await CreateInventoryItemAsync(productId);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync($"/api/v1/inventory-items/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(productId, document.RootElement.GetProperty("productId").GetGuid());
        Assert.Equal("Keyboard", document.RootElement.GetProperty("productName").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInventoryItem_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/inventory-items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateInventoryItem_WhenItemExists_ReturnsUpdatedItemAndPersistsChanges()
    {
        var productId = Guid.NewGuid();

        var createResponse = await CreateInventoryItemAsync(productId);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/inventory-items/{productId}",
            new
            {
                productName = "Updated Keyboard",
                availableQuantity = 75
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var json = await updateResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("Updated Keyboard", document.RootElement.GetProperty("productName").GetString());
        Assert.Equal(75, document.RootElement.GetProperty("availableQuantity").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var item = await dbContext.InventoryItems.SingleAsync();

        Assert.Equal("Updated Keyboard", item.ProductName);
        Assert.Equal(75, item.AvailableQuantity);
    }

    private async Task<HttpResponseMessage> CreateInventoryItemAsync(Guid productId)
    {
        return await _client.PostAsJsonAsync(
            "/api/v1/inventory-items",
            new
            {
                productId,
                productName = "Keyboard",
                availableQuantity = 50
            });
    }
}