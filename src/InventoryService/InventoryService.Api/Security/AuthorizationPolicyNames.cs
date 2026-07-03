namespace InventoryService.Api.Security;

public static class AuthorizationPolicyNames
{
    public const string SupportOrAdmin = "SupportOrAdmin";
    public const string CanManageInventory = "CanManageInventory";
}