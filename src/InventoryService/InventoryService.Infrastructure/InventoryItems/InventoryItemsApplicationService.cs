using FluentValidation;
using InventoryService.Application.Common.Abstractions;
using InventoryService.Application.Common.Exceptions;
using InventoryService.Application.Common.Pagination;
using InventoryService.Application.InventoryItems.Abstractions;
using InventoryService.Application.InventoryItems.Contracts;
using InventoryService.Domain.Inventory;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.InventoryItems;

public sealed class InventoryItemsApplicationService(
    InventoryDbContext dbContext,
    IClock clock,
    IValidator<CreateInventoryItemRequest> createInventoryItemRequestValidator,
    IValidator<ListInventoryItemsRequest> listInventoryItemsRequestValidator,
    IValidator<UpdateInventoryItemRequest> updateInventoryItemRequestValidator) : IInventoryItemsService
{
    private readonly InventoryDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly IValidator<CreateInventoryItemRequest> 
        _createInventoryItemRequestValidator = createInventoryItemRequestValidator;
    private readonly IValidator<ListInventoryItemsRequest> 
        _listInventoryItemsRequestValidator = listInventoryItemsRequestValidator;
    private readonly IValidator<UpdateInventoryItemRequest> 
        _updateInventoryItemRequestValidator = updateInventoryItemRequestValidator;

    public async Task<InventoryItemResponse> CreateAsync(
        CreateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _createInventoryItemRequestValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var productIdAlreadyExists = await _dbContext.InventoryItems
            .AsNoTracking()
            .AnyAsync(
                inventoryItem => inventoryItem.ProductId == request.ProductId,
                cancellationToken);

        if (productIdAlreadyExists)
        {
            throw new DuplicateInventoryItemException(request.ProductId);
        }

        var now = _clock.UtcNow;

        var inventoryItem = new InventoryItem(
            id: Guid.NewGuid(),
            productId: request.ProductId,
            productName: request.ProductName!,
            availableQuantity: request.AvailableQuantity,
            createdAtUtc: now);

        _dbContext.InventoryItems.Add(inventoryItem);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(inventoryItem);
    }

    public async Task<InventoryItemResponse?> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            return null;
        }

        var inventoryItem = await _dbContext.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.ProductId == productId,
                cancellationToken);

        return inventoryItem is null
            ? null
            : MapToResponse(inventoryItem);
    }

    public async Task<PagedResult<InventoryItemResponse>> ListAsync(
        ListInventoryItemsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationResult = await _listInventoryItemsRequestValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var query = _dbContext.InventoryItems
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ProductName))
        {
            var productName = request.ProductName.Trim();

            query = query.Where(inventoryItem =>
                inventoryItem.ProductName.Contains(productName));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(
            query,
            request.SortBy!,
            request.SortDirection!);

        var inventoryItems = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = inventoryItems
            .Select(MapToResponse)
            .ToList();

        return new PagedResult<InventoryItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<InventoryItemResponse> UpdateAsync(
        Guid productId,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (productId == Guid.Empty)
        {
            throw new InventoryItemNotFoundException(productId);
        }

        var validationResult = await _updateInventoryItemRequestValidator.ValidateAsync(
            request,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var inventoryItem = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(
                item => item.ProductId == productId,
                cancellationToken);

        if (inventoryItem is null)
        {
            throw new InventoryItemNotFoundException(productId);
        }

        inventoryItem.Update(
            productName: request.ProductName!,
            availableQuantity: request.AvailableQuantity,
            updatedAtUtc: _clock.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(inventoryItem);
    }

    private static IQueryable<InventoryItem> ApplySorting(
        IQueryable<InventoryItem> query,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(
            sortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "createdatutc" => descending
                ? query.OrderByDescending(inventoryItem => inventoryItem.CreatedAtUtc)
                : query.OrderBy(inventoryItem => inventoryItem.CreatedAtUtc),

            "updatedatutc" => descending
                ? query.OrderByDescending(inventoryItem => inventoryItem.UpdatedAtUtc)
                : query.OrderBy(inventoryItem => inventoryItem.UpdatedAtUtc),

            "productname" => descending
                ? query.OrderByDescending(inventoryItem => inventoryItem.ProductName)
                : query.OrderBy(inventoryItem => inventoryItem.ProductName),

            "availablequantity" => descending
                ? query.OrderByDescending(inventoryItem => inventoryItem.AvailableQuantity)
                : query.OrderBy(inventoryItem => inventoryItem.AvailableQuantity),

            "reservedquantity" => descending
                ? query.OrderByDescending(inventoryItem => inventoryItem.ReservedQuantity)
                : query.OrderBy(inventoryItem => inventoryItem.ReservedQuantity),

            _ => descending
                ? query.OrderByDescending(inventoryItem => inventoryItem.CreatedAtUtc)
                : query.OrderBy(inventoryItem => inventoryItem.CreatedAtUtc)
        };
    }

    private static InventoryItemResponse MapToResponse(InventoryItem inventoryItem)
    {
        return new InventoryItemResponse
        {
            ProductId = inventoryItem.ProductId,
            ProductName = inventoryItem.ProductName,
            AvailableQuantity = inventoryItem.AvailableQuantity,
            ReservedQuantity = inventoryItem.ReservedQuantity,
            CreatedAtUtc = inventoryItem.CreatedAtUtc,
            UpdatedAtUtc = inventoryItem.UpdatedAtUtc
        };
    }
}