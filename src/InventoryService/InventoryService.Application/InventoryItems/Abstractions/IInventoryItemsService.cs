using InventoryService.Application.InventoryItems.Contracts;
using InventoryService.Application.Common.Pagination;


namespace InventoryService.Application.InventoryItems.Abstractions;

public interface IInventoryItemsService
{
    Task<InventoryItemResponse> CreateAsync(
        CreateInventoryItemRequest request,
        CancellationToken cancellationToken);

    Task<InventoryItemResponse?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken);

    Task<PagedResult<InventoryItemResponse>> ListAsync(
        ListInventoryItemsRequest request,
        CancellationToken cancellationToken);

    Task<InventoryItemResponse> UpdateAsync(
        Guid productId,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken);
}