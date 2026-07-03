namespace ApiGateway.Security;

public static class RateLimitingPolicyNames
{
    public const string OrderCreationLimit = "OrderCreationLimit";
    public const string AuthenticatedUserLimit = "AuthenticatedUserLimit";
    public const string AdminEndpointLimit = "AdminEndpointLimit";
}