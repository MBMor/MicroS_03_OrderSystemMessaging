using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderSystem.Contracts.IntegrationEvents;
using OrdersService.Api.PostgresIntegrationTests.Common;
using OrdersService.Domain.Orders;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;

namespace OrdersService.Api.PostgresIntegrationTests.Orders;

public sealed class OrdersApiTests(OrdersApiFactory factory) : IClassFixture<OrdersApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrdersApiFactory _factory = factory;
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
    public async Task CreateOrder_WithValidRequest_ReturnsCreatedAndStoresOrderAndOutboxMessage()
    {
        var productId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerName = "John Doe",
                customerEmail = "john.doe@example.com",
                items = new[]
                {
                    new
                    {
                        productId,
                        productName = "Keyboard",
                        quantity = 2
                    }
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var orderId = document.RootElement.GetProperty("id").GetGuid();
        var status = document.RootElement.GetProperty("status").GetString();

        Assert.NotEqual(Guid.Empty, orderId);
        Assert.Equal("PendingStockReservation", status);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var order = await dbContext.Orders
            .Include(existingOrder => existingOrder.Items)
            .SingleAsync();

        Assert.Equal(orderId, order.Id);
        Assert.Equal(OrderStatus.PendingStockReservation, order.Status);
        Assert.Single(order.Items);

        var outboxMessage = await dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(IntegrationEventTypes.OrderCreated, outboxMessage.EventType);
        Assert.Equal(RabbitMqRoutingKeys.OrderCreated, outboxMessage.RoutingKey);
        Assert.Equal(OutboxStatus.Pending, outboxMessage.Status);
        Assert.Contains(orderId.ToString(), outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrderById_WhenOrderExists_ReturnsOrder()
    {
        var createResponse = await CreateOrderAsync();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var orderId = createDocument.RootElement.GetProperty("id").GetGuid();

        var response = await _client.GetAsync($"/api/v1/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(orderId, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("PendingStockReservation", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrderById_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpResponseMessage> CreateOrderAsync()
    {
        return await _client.PostAsJsonAsync(
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
                        quantity = 2
                    }
                }
            },
            JsonOptions);
    }
}