using System.Net;
using ApiGateway.IntegrationTests.Common;

namespace ApiGateway.IntegrationTests;

public sealed class GatewayInternalEndpointProtectionTests(ApiGatewayFactory factory)
    : IClassFixture<ApiGatewayFactory>
{
    private readonly ApiGatewayFactory _factory = factory;

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("/swagger")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/admin")]
    [InlineData("/internal/status")]
    public async Task Gateway_DoesNotExposeRootInternalOrDiagnosticPaths(string path)
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNotRoutedToDownstream(response);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("/api/v1/orders/swagger")]
    [InlineData("/api/v1/orders/internal/status")]
    [InlineData("/api/v1/orders/health/ready")]
    [InlineData("/api/v1/orders/health/live")]
    public async Task Gateway_DoesNotExposeOrdersInternalPaths(string path)
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNotRoutedToDownstream(response);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("/api/v1/inventory-items/swagger")]
    [InlineData("/api/v1/inventory-items/internal/status")]
    [InlineData("/api/v1/inventory-items/health/ready")]
    [InlineData("/api/v1/inventory-items/health/live")]
    public async Task Gateway_DoesNotExposeInventoryInternalPaths(string path)
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNotRoutedToDownstream(response);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("/api/v1/notifications/swagger")]
    [InlineData("/api/v1/notifications/internal/status")]
    [InlineData("/api/v1/notifications/health/ready")]
    [InlineData("/api/v1/notifications/health/live")]
    public async Task Gateway_DoesNotExposeNotificationsInternalPaths(string path)
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNotRoutedToDownstream(response);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("/api/v1/orders")]
    [InlineData("/api/v1/inventory-items")]
    [InlineData("/api/v1/notifications")]
    public async Task Gateway_DoesNotAllowDeleteForPublicResources(string path)
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.DeleteAsync(path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        AssertNotRoutedToDownstream(response);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Gateway_HealthReady_IsGatewayEndpoint_NotDownstreamProxy()
    {
        using var client = _factory.CreateAdminClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNotRoutedToDownstream(response);
    }

    private static void AssertNotRoutedToDownstream(HttpResponseMessage response)
    {
        Assert.False(
            response.Headers.Contains("X-Fake-Service"),
            "Response contains X-Fake-Service header, which means the request was proxied to a downstream service.");
    }
}