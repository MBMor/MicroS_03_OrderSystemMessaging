using FluentValidation;
using InventoryService.Application.InventoryItems.Contracts;
using InventoryService.Domain.Inventory;

namespace InventoryService.Application.InventoryItems.Validation;

public sealed class ListInventoryItemsRequestValidator : AbstractValidator<ListInventoryItemsRequest>
{
    private static readonly HashSet<string> AllowedSortByValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAtUtc",
        "updatedAtUtc",
        "productName",
        "availableQuantity",
        "reservedQuantity"
    };

    private static readonly HashSet<string> AllowedSortDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc",
        "desc"
    };

    public ListInventoryItemsRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThan(0);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(request => request.ProductName)
            .MaximumLength(InventoryItem.ProductNameMaxLength)
            .When(request => !string.IsNullOrWhiteSpace(request.ProductName));

        RuleFor(request => request.SortBy)
            .Must(sortBy => !string.IsNullOrWhiteSpace(sortBy) && AllowedSortByValues.Contains(sortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", AllowedSortByValues)}.");

        RuleFor(request => request.SortDirection)
            .Must(sortDirection => !string.IsNullOrWhiteSpace(sortDirection) && AllowedSortDirections.Contains(sortDirection))
            .WithMessage("SortDirection must be either 'asc' or 'desc'.");
    }
}