using ApiGateway.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHealthChecks("/health");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapReverseProxy()
   .RequireAuthorization();

app.Run();