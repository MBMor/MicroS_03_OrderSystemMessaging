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

        app.MapGet("/health/ready", () => Results.Ok("Healthy"));

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
            (HttpResponse response) => JsonResponse(response, "orders", "orders-list"));

        app.MapGet(
            "/api/v1/orders/{id:guid}",
            (HttpResponse response) => JsonResponse(response, "orders", "orders-get-by-id"));

        app.MapPost(
            "/api/v1/orders",
            (HttpResponse response) => CreatedResponse(response, "orders", "orders-create"));
    }

    private static void MapInventoryRoutes(WebApplication app)
    {
        app.MapGet(
            "/api/v1/inventory-items",
            (HttpResponse response) => JsonResponse(response, "inventory", "inventory-list"));

        app.MapGet(
            "/api/v1/inventory-items/{productId:guid}",
            (HttpResponse response) => JsonResponse(response, "inventory", "inventory-get-by-product-id"));

        app.MapPost(
            "/api/v1/inventory-items",
            (HttpResponse response) => CreatedResponse(response, "inventory", "inventory-create"));

        app.MapPut(
            "/api/v1/inventory-items/{productId:guid}",
            (HttpResponse response) => NoContentResponse(response, "inventory"));
    }

    private static void MapNotificationsRoutes(WebApplication app)
    {
        app.MapGet(
            "/api/v1/notifications",
            (HttpResponse response) => JsonResponse(response, "notifications", "notifications-list"));

        app.MapGet(
            "/api/v1/notifications/{id:guid}",
            (HttpResponse response) => JsonResponse(response, "notifications", "notifications-get-by-id"));
    }

    private static IResult JsonResponse(HttpResponse response, string service, string endpoint)
    {
        response.Headers["X-Fake-Service"] = service;

        return Results.Json(new
        {
            service,
            endpoint
        });
    }

    private static IResult CreatedResponse(HttpResponse response, string service, string endpoint)
    {
        response.Headers["X-Fake-Service"] = service;

        return Results.Created(
            $"/fake/{endpoint}",
            new
            {
                service,
                endpoint
            });
    }

    private static IResult NoContentResponse(HttpResponse response, string service)
    {
        response.Headers["X-Fake-Service"] = service;

        return Results.NoContent();
    }
}