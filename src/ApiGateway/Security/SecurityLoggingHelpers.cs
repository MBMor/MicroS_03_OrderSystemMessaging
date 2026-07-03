namespace ApiGateway.Security;

public static class SecurityLoggingHelpers
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