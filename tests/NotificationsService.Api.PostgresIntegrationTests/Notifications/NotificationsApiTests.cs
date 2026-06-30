using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Api.PostgresIntegrationTests.Common;
using NotificationsService.Domain.Notifications;
using NotificationsService.Infrastructure.Persistence;
using OrderSystem.Contracts.IntegrationEvents;

namespace NotificationsService.Api.PostgresIntegrationTests.Notifications;

public sealed class NotificationsApiTests(NotificationsApiFactory factory) : IClassFixture<NotificationsApiFactory>, IAsyncLifetime
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly NotificationsApiFactory _factory = factory;
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
    public async Task GetNotificationById_WhenNotificationExists_ReturnsNotification()
    {
        var notification = await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.OrderCreated,
            recipient: "john.doe@example.com",
            subject: "Order was created",
            body: "Your order was created.");

        var response = await _client.GetAsync($"/api/v1/notifications/{notification.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(notification.Id, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(notification.SourceEventId, document.RootElement.GetProperty("sourceEventId").GetGuid());
        Assert.Equal(notification.SourceEventType, document.RootElement.GetProperty("sourceEventType").GetString());
        Assert.Equal(notification.Recipient, document.RootElement.GetProperty("recipient").GetString());
        Assert.Equal(notification.Subject, document.RootElement.GetProperty("subject").GetString());
        Assert.Equal(notification.Body, document.RootElement.GetProperty("body").GetString());
        Assert.Equal("Created", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNotificationById_WhenNotificationDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/notifications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListNotifications_WhenNotificationsExist_ReturnsPagedResult()
    {
        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.OrderCreated,
            recipient: "customer.one@example.com",
            subject: "Order was created",
            body: "Your order was created.");

        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.StockReserved,
            recipient: "customer.two@example.com",
            subject: "Stock reserved",
            body: "Stock was reserved for your order.");

        var response = await _client.GetAsync("/api/v1/notifications?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, document.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("totalCount").GetInt32());

        var items = document.RootElement.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListNotifications_WithSourceEventTypeFilter_ReturnsOnlyMatchingNotifications()
    {
        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.OrderCreated,
            recipient: "customer.one@example.com",
            subject: "Order was created",
            body: "Your order was created.");

        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.StockReserved,
            recipient: "customer.two@example.com",
            subject: "Stock reserved",
            body: "Stock was reserved for your order.");

        var response = await _client.GetAsync(
            $"/api/v1/notifications?sourceEventType={IntegrationEventTypes.StockReserved}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());

        var items = document.RootElement.GetProperty("items");

        Assert.Single(items.EnumerateArray());

        var firstItem = items[0];

        Assert.Equal(IntegrationEventTypes.StockReserved, firstItem.GetProperty("sourceEventType").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListNotifications_WithStatusFilter_ReturnsOnlyMatchingNotifications()
    {
        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.OrderCreated,
            recipient: "customer.one@example.com",
            subject: "Order was created",
            body: "Your order was created.",
            status: NotificationStatus.Created);

        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.StockReserved,
            recipient: "customer.two@example.com",
            subject: "Stock reserved",
            body: "Stock was reserved for your order.",
            status: NotificationStatus.Processed);

        var response = await _client.GetAsync("/api/v1/notifications?status=Processed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());

        var items = document.RootElement.GetProperty("items");

        Assert.Single(items.EnumerateArray());

        var firstItem = items[0];

        Assert.Equal("Processed", firstItem.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListNotifications_WithPagination_ReturnsRequestedPage()
    {
        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.OrderCreated,
            recipient: "customer.one@example.com",
            subject: "First notification",
            body: "First body.",
            createdAtUtc: UtcNow.AddMinutes(-2));

        await SeedNotificationAsync(
            sourceEventType: IntegrationEventTypes.StockReserved,
            recipient: "customer.two@example.com",
            subject: "Second notification",
            body: "Second body.",
            createdAtUtc: UtcNow.AddMinutes(-1));

        var response = await _client.GetAsync(
            "/api/v1/notifications?page=2&pageSize=1&sortBy=createdAtUtc&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(2, document.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("totalCount").GetInt32());

        var items = document.RootElement.GetProperty("items");

        Assert.Single(items.EnumerateArray());

        var firstItem = items[0];

        Assert.Equal("Second notification", firstItem.GetProperty("subject").GetString());
    }

    private async Task<Notification> SeedNotificationAsync(
        string sourceEventType,
        string recipient,
        string subject,
        string body,
        NotificationStatus status = NotificationStatus.Created,
        DateTime? createdAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var notification = new Notification(
            id: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            sourceEventType: sourceEventType,
            recipient: recipient,
            subject: subject,
            body: body,
            createdAtUtc: createdAtUtc ?? UtcNow);

        if (status == NotificationStatus.Processed)
        {
            notification.MarkProcessed();
        }

        if (status == NotificationStatus.Failed)
        {
            notification.MarkFailed();
        }

        dbContext.Notifications.Add(notification);

        await dbContext.SaveChangesAsync();

        return notification;
    }
}