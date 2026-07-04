using System.Net;
using NotificationsService.Api.PostgresIntegrationTests.Common;

namespace NotificationsService.Api.PostgresIntegrationTests.Notifications;

public sealed class NotificationsForbiddenAuthorizationTests(NotificationsApiFactory factory)
    : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory _factory = factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNotifications_WithCustomerRole_ReturnsForbidden()
    {
        using var client = _factory.CreateCustomerClient();

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNotificationById_WithCustomerRole_ReturnsForbidden()
    {
        using var client = _factory.CreateCustomerClient();

        var response = await client.GetAsync($"/api/v1/notifications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}