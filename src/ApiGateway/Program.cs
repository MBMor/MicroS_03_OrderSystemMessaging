using System.Threading.RateLimiting;
using ApiGateway.Health;
using ApiGateway.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient("health-checks");

builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("API Gateway is running."),
        tags: ["live"])
    .Add(new HealthCheckRegistration(
        "orders-api",
        serviceProvider => CreateUrlHealthCheck(
            serviceProvider,
            builder.Configuration,
            "health-checks",
            "HealthChecks:OrdersApiReadyUrl"),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]))
    .Add(new HealthCheckRegistration(
        "inventory-api",
        serviceProvider => CreateUrlHealthCheck(
            serviceProvider,
            builder.Configuration,
            "health-checks",
            "HealthChecks:InventoryApiReadyUrl"),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]))
    .Add(new HealthCheckRegistration(
        "notifications-api",
        serviceProvider => CreateUrlHealthCheck(
            serviceProvider,
            builder.Configuration,
            "health-checks",
            "HealthChecks:NotificationsApiReadyUrl"),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]))
    .Add(new HealthCheckRegistration(
        "keycloak",
        serviceProvider => CreateUrlHealthCheck(
            serviceProvider,
            builder.Configuration,
            "health-checks",
            "HealthChecks:KeycloakRealmUrl"),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]));

var jwtAuthority = builder.Configuration["Jwt:Authority"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtValidIssuer = builder.Configuration["Jwt:ValidIssuer"];
var requireHttpsMetadata = builder.Configuration.GetValue<bool>("Jwt:RequireHttpsMetadata");

if (string.IsNullOrWhiteSpace(jwtAuthority))
{
    throw new InvalidOperationException("JWT configuration value 'Jwt:Authority' is missing.");
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("JWT configuration value 'Jwt:Audience' is missing.");
}

if (string.IsNullOrWhiteSpace(jwtValidIssuer))
{
    throw new InvalidOperationException("JWT configuration value 'Jwt:ValidIssuer' is missing.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.Authority = jwtAuthority;
        options.Audience = jwtAudience;
        options.RequireHttpsMetadata = requireHttpsMetadata;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtValidIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(
        AuthorizationPolicyNames.AuthenticatedUser,
        policy => policy.RequireAuthenticatedUser())
    .AddPolicy(
        AuthorizationPolicyNames.CustomerOnly,
        policy => policy.RequireRole(RoleNames.Customer))
    .AddPolicy(
        AuthorizationPolicyNames.SupportOrAdmin,
        policy => policy.RequireRole(RoleNames.Support, RoleNames.Admin))
    .AddPolicy(
        AuthorizationPolicyNames.AdminOnly,
        policy => policy.RequireRole(RoleNames.Admin))
    .AddPolicy(
        AuthorizationPolicyNames.CanCreateOrder,
        policy => policy.RequireAuthenticatedUser())
    .AddPolicy(
        AuthorizationPolicyNames.CanManageInventory,
        policy => policy.RequireRole(RoleNames.Admin))
    .AddPolicy(
        AuthorizationPolicyNames.CanReadNotifications,
        policy => policy.RequireRole(RoleNames.Support, RoleNames.Admin));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitingPolicyNames.OrderCreationLimit, httpContext =>
    {
        var partitionKey = GetRateLimitPartitionKey(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy(RateLimitingPolicyNames.AuthenticatedUserLimit, httpContext =>
    {
        var partitionKey = GetRateLimitPartitionKey(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy(RateLimitingPolicyNames.AdminEndpointLimit, httpContext =>
    {
        var partitionKey = GetRateLimitPartitionKey(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("live")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.MapReverseProxy()
   .RequireAuthorization();

app.Run();

static string GetRateLimitPartitionKey(HttpContext httpContext)
{
    var username = httpContext.User.Identity?.Name;

    if (!string.IsNullOrWhiteSpace(username))
    {
        return $"user:{username}";
    }

    var remoteIpAddress = httpContext.Connection.RemoteIpAddress?.ToString();

    if (!string.IsNullOrWhiteSpace(remoteIpAddress))
    {
        return $"ip:{remoteIpAddress}";
    }

    return "anonymous";
}

static IHealthCheck CreateUrlHealthCheck(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    string httpClientName,
    string configurationKey)
{
    var url = configuration[configurationKey];

    if (string.IsNullOrWhiteSpace(url))
    {
        throw new InvalidOperationException(
            $"Health check configuration value '{configurationKey}' is missing.");
    }

    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

    return new UrlHealthCheck(httpClientFactory.CreateClient(httpClientName), url);
}