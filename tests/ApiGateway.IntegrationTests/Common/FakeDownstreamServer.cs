using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.IntegrationTests.Common;

public sealed class FakeDownstreamServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FakeDownstreamServer(WebApplication app, string baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress.TrimEnd('/');
    }

    public string BaseAddress { get; }

    public static async Task<FakeDownstreamServer> StartAsync(string serviceName)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Fake-Service"] = serviceName;
            await next(context);
        });

        app.MapGet("/health/live", () => Results.Ok("Healthy"));
        app.MapGet("/health/ready", () => Results.Ok("Healthy"));

        app.MapGet("/swagger", () => Results.Ok($"Fake {serviceName} Swagger UI"));
        app.MapGet("/swagger/v1/swagger.json", () => Results.Json(new
        {
            service = serviceName,
            document = "swagger"
        }));

        app.MapGet("/internal/status", () => Results.Ok(new
        {
            service = serviceName,
            status = "internal"
        }));

        MapServiceRoutes(app, serviceName);

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();

        var baseAddress = addressesFeature?.Addresses.SingleOrDefault();

        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new InvalidOperationException("Fake downstream server did not expose a listening address.");
        }

        return new FakeDownstreamServer(app, baseAddress);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static void MapServiceRoutes(WebApplication app, string serviceName)
    {
        switch (serviceName)
        {
            case "orders":
                MapOrdersRoutes(app);
                break;

            case "inventory":
                MapInventoryRoutes(app);
                break;

            case "notifications":
                MapNotificationsRoutes(app);
                break;

            case "keycloak":
                app.MapGet("/realms/order-system", () => Results.Ok(new { realm = "order-system" }));
                break;

            default:
                throw new InvalidOperationException($"Unknown fake downstream service '{serviceName}'.");
        }
    }

    private static void MapOrdersRoutes(WebApplication app)
    {
        app.MapGet(
            "/api/v1/orders",
            () => Results.Json(new
            {
                service = "orders",
                endpoint = "orders-list"
            }));

        app.MapGet(
            "/api/v1/orders/{id:guid}",
            () => Results.Json(new
            {
                service = "orders",
                endpoint = "orders-get-by-id"
            }));

        app.MapPost(
            "/api/v1/orders",
            () => Results.Created(
                "/fake/orders-create",
                new
                {
                    service = "orders",
                    endpoint = "orders-create"
                }));

        app.MapGet(
            "/api/v1/orders/internal/status",
            () => Results.Ok(new
            {
                service = "orders",
                endpoint = "orders-internal-status"
            }));

        app.MapGet(
            "/api/v1/orders/swagger",
            () => Results.Ok("Fake Orders Swagger"));
    }

    private static void MapInventoryRoutes(WebApplication app)
    {
        app.MapGet(
            "/api/v1/inventory-items",
            () => Results.Json(new
            {
                service = "inventory",
                endpoint = "inventory-list"
            }));

        app.MapGet(
            "/api/v1/inventory-items/{productId:guid}",
            () => Results.Json(new
            {
                service = "inventory",
                endpoint = "inventory-get-by-product-id"
            }));

        app.MapPost(
            "/api/v1/inventory-items",
            () => Results.Created(
                "/fake/inventory-create",
                new
                {
                    service = "inventory",
                    endpoint = "inventory-create"
                }));

        app.MapPut(
            "/api/v1/inventory-items/{productId:guid}",
            () => Results.NoContent());

        app.MapGet(
            "/api/v1/inventory-items/internal/status",
            () => Results.Ok(new
            {
                service = "inventory",
                endpoint = "inventory-internal-status"
            }));

        app.MapGet(
            "/api/v1/inventory-items/swagger",
            () => Results.Ok("Fake Inventory Swagger"));
    }

    private static void MapNotificationsRoutes(WebApplication app)
    {
        app.MapGet(
            "/api/v1/notifications",
            () => Results.Json(new
            {
                service = "notifications",
                endpoint = "notifications-list"
            }));

        app.MapGet(
            "/api/v1/notifications/{id:guid}",
            () => Results.Json(new
            {
                service = "notifications",
                endpoint = "notifications-get-by-id"
            }));

        app.MapGet(
            "/api/v1/notifications/internal/status",
            () => Results.Ok(new
            {
                service = "notifications",
                endpoint = "notifications-internal-status"
            }));

        app.MapGet(
            "/api/v1/notifications/swagger",
            () => Results.Ok("Fake Notifications Swagger"));
    }
}