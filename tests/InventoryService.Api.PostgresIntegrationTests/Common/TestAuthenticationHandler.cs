using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryService.Api.PostgresIntegrationTests.Common;

public sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "Test";
    public const string HeaderName = "X-Test-Auth";
    public const string HeaderValue = "true";

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

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "integration-test-user"),
            new Claim(ClaimTypes.Name, "integration-test-user"),
            new Claim("preferred_username", "integration-test-user"),

            new Claim(ClaimTypes.Role, "customer"),
            new Claim(ClaimTypes.Role, "support"),
            new Claim(ClaimTypes.Role, "admin"),

            new Claim("roles", "customer"),
            new Claim("roles", "support"),
            new Claim("roles", "admin")
        };

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
}