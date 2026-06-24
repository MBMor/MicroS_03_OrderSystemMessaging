using FluentValidation;
using InventoryService.Application.InventoryItems.Contracts;
using InventoryService.Domain.Inventory;

namespace InventoryService.Application.InventoryItems.Validation;

public sealed class CreateInventoryItemRequestValidator : AbstractValidator<CreateInventoryItemRequest>
{
    public CreateInventoryItemRequestValidator()
    {
        RuleFor(request => request.ProductId)
            .NotEmpty();

        RuleFor(request => request.ProductName)
            .NotEmpty()
            .MaximumLength(InventoryItem.ProductNameMaxLength);

        RuleFor(request => request.AvailableQuantity)
            .GreaterThanOrEqualTo(0);
    }
}