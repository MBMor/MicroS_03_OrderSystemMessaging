namespace OrdersService.Api.Security;

public static class AuthorizationPolicyNames
{
    public const string AuthenticatedUser = "AuthenticatedUser";
    public const string SupportOrAdmin = "SupportOrAdmin";
    public const string CanCreateOrder = "CanCreateOrder";
}