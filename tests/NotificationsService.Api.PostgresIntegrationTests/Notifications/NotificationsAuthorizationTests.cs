using System.Net;
using NotificationsService.Api.PostgresIntegrationTests.Common;

namespace NotificationsService.Api.PostgresIntegrationTests.Notifications;

public sealed class NotificationsAuthorizationTests(NotificationsApiFactory factory)
    : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory _factory = factory;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNotifications_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNotificationById_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateUnauthenticatedClient();

        var response = await client.GetAsync($"/api/v1/notifications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}