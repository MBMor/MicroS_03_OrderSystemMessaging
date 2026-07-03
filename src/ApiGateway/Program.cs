using System.Diagnostics;
using System.Threading.RateLimiting;
using ApiGateway.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHealthChecks();

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

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = SecurityLoggingHelpers.GetSecurityLogger(context.HttpContext);

                logger.LogWarning(
                    "JWT authentication failed. Path: {Path}. Method: {Method}. ErrorType: {ErrorType}. TraceId: {TraceId}",
                    context.HttpContext.Request.Path.Value,
                    context.HttpContext.Request.Method,
                    context.Exception.GetType().Name,
                    context.HttpContext.TraceIdentifier);

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = SecurityLoggingHelpers.GetSecurityLogger(context.HttpContext);

                logger.LogInformation(
                    "JWT authentication challenge returned. Path: {Path}. Method: {Method}. TraceId: {TraceId}",
                    context.HttpContext.Request.Path.Value,
                    context.HttpContext.Request.Method,
                    context.HttpContext.TraceIdentifier);

                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                var logger = SecurityLoggingHelpers.GetSecurityLogger(context.HttpContext);

                logger.LogWarning(
                    "JWT authorization forbidden. User: {User}. Path: {Path}. Method: {Method}. TraceId: {TraceId}",
                    SecurityLoggingHelpers.GetUserName(context.HttpContext),
                    context.HttpContext.Request.Path.Value,
                    context.HttpContext.Request.Method,
                    context.HttpContext.TraceIdentifier);

                return Task.CompletedTask;
            }
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

    options.OnRejected = (context, _) =>
    {
        var httpContext = context.HttpContext;
        var logger = SecurityLoggingHelpers.GetSecurityLogger(httpContext);

        logger.LogWarning(
            "Rate limit rejected request. User: {User}. Path: {Path}. Method: {Method}. TraceId: {TraceId}",
            SecurityLoggingHelpers.GetUserName(httpContext),
            httpContext.Request.Path.Value,
            httpContext.Request.Method,
            httpContext.TraceIdentifier);

        return ValueTask.CompletedTask;
    };

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

app.MapHealthChecks("/health");

app.UseSecurityRequestLogging();

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

static class SecurityLoggingHelpers
{
    public static ILogger GetSecurityLogger(HttpContext httpContext)
    {
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();

        return loggerFactory.CreateLogger("ApiGateway.Security");
    }

    public static string GetUserName(HttpContext httpContext)
    {
        return httpContext.User.Identity?.Name ?? "anonymous";
    }
}

static class SecurityRequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityRequestLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var logger = SecurityLoggingHelpers.GetSecurityLogger(context);
            var stopwatch = Stopwatch.StartNew();

            logger.LogInformation(
                "Gateway request started. Method: {Method}. Path: {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.TraceIdentifier);

            await next(context);

            stopwatch.Stop();

            logger.LogInformation(
                "Gateway request completed. Method: {Method}. Path: {Path}. StatusCode: {StatusCode}. ElapsedMs: {ElapsedMs}. User: {User}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                SecurityLoggingHelpers.GetUserName(context),
                context.TraceIdentifier);
        });
    }
}