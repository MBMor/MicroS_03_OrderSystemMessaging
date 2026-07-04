using ApiGateway.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.IntegrationTests.Common;

public sealed class ApiGatewayFactory : WebApplicationFactory<ApiAssemblyMarker>
{
    private readonly FakeDownstreamServer _ordersServer;
    private readonly FakeDownstreamServer _inventoryServer;
    private readonly FakeDownstreamServer _notificationsServer;
    private readonly FakeDownstreamServer _keycloakServer;

    public ApiGatewayFactory()
    {
        _ordersServer = FakeDownstreamServer.StartAsync("orders").GetAwaiter().GetResult();
        _inventoryServer = FakeDownstreamServer.StartAsync("inventory").GetAwaiter().GetResult();
        _notificationsServer = FakeDownstreamServer.StartAsync("notifications").GetAwaiter().GetResult();
        _keycloakServer = FakeDownstreamServer.StartAsync("keycloak").GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var testConfiguration = new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = "http://localhost/test-authority",
                ["Jwt:Audience"] = "order-system-api",
                ["Jwt:ValidIssuer"] = "http://localhost/test-authority",
                ["Jwt:RequireHttpsMetadata"] = "false",

                ["HealthChecks:OrdersApiReadyUrl"] = $"{_ordersServer.BaseAddress}/health/ready",
                ["HealthChecks:InventoryApiReadyUrl"] = $"{_inventoryServer.BaseAddress}/health/ready",
                ["HealthChecks:NotificationsApiReadyUrl"] = $"{_notificationsServer.BaseAddress}/health/ready",
                ["HealthChecks:KeycloakRealmUrl"] = $"{_keycloakServer.BaseAddress}/realms/order-system",

                ["ReverseProxy:Clusters:orders-cluster:Destinations:orders-api:Address"] = $"{_ordersServer.BaseAddress}/",
                ["ReverseProxy:Clusters:inventory-cluster:Destinations:inventory-api:Address"] = $"{_inventoryServer.BaseAddress}/",
                ["ReverseProxy:Clusters:notifications-cluster:Destinations:notifications-api:Address"] = $"{_notificationsServer.BaseAddress}/"
            };

            configurationBuilder.AddInMemoryCollection(testConfiguration);
        });

        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultForbidScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultScheme = TestAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.HeaderName,
            TestAuthenticationHandler.HeaderValue);

        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.RolesHeaderName,
                string.Join(',', roles));
        }

        return client;
    }

    public HttpClient CreateCustomerClient()
    {
        return CreateAuthenticatedClient("customer");
    }

    public HttpClient CreateSupportClient()
    {
        return CreateAuthenticatedClient("support");
    }

    public HttpClient CreateAdminClient()
    {
        return CreateAuthenticatedClient("admin");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ordersServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _inventoryServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _notificationsServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _keycloakServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}