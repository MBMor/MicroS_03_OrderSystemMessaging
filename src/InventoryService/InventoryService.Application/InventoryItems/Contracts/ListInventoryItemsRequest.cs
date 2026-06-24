namespace InventoryService.Application.InventoryItems.Contracts;

public sealed record ListInventoryItemsRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? ProductName { get; init; }

    public string? SortBy { get; init; } = "createdAtUtc";

    public string? SortDirection { get; init; } = "desc";
}