namespace ApiGateway.Security;

public static class AuthorizationPolicyNames
{
    public const string AuthenticatedUser = "AuthenticatedUser";
    public const string CustomerOnly = "CustomerOnly";
    public const string SupportOrAdmin = "SupportOrAdmin";
    public const string AdminOnly = "AdminOnly";
    public const string CanCreateOrder = "CanCreateOrder";
    public const string CanManageInventory = "CanManageInventory";
    public const string CanReadNotifications = "CanReadNotifications";
}