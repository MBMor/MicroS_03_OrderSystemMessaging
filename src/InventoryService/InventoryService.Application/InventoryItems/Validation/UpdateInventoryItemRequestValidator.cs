using FluentValidation;
using InventoryService.Application.InventoryItems.Contracts;
using InventoryService.Domain.Inventory;

namespace InventoryService.Application.InventoryItems.Validation;

public sealed class UpdateInventoryItemRequestValidator : AbstractValidator<UpdateInventoryItemRequest>
{
    public UpdateInventoryItemRequestValidator()
    {
        RuleFor(request => request.ProductName)
            .NotEmpty()
            .MaximumLength(InventoryItem.ProductNameMaxLength);

        RuleFor(request => request.AvailableQuantity)
            .GreaterThanOrEqualTo(0);
    }
}