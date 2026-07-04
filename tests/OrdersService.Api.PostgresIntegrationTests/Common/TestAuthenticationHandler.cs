using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrdersService.Api.PostgresIntegrationTests.Common;

public sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "Test";
    public const string HeaderName = "X-Test-Auth";
    public const string HeaderValue = "true";
    public const string RolesHeaderName = "X-Test-Roles";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues) ||
            !headerValues.Any(value => string.Equals(value, HeaderValue, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roles = GetRolesFromRequest();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "integration-test-user"),
            new(ClaimTypes.Name, "integration-test-user"),
            new("preferred_username", "integration-test-user")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("roles", role));
        }

        var identity = new ClaimsIdentity(
            claims,
            AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private IReadOnlyCollection<string> GetRolesFromRequest()
    {
        if (!Request.Headers.TryGetValue(RolesHeaderName, out var roleHeaderValues))
        {
            return ["customer", "support", "admin"];
        }

        var roles = roleHeaderValues
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roles.Length > 0
            ? roles
            : ["customer", "support", "admin"];
    }
}