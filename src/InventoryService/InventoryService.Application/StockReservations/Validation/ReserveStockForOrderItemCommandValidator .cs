using FluentValidation;
using InventoryService.Application.StockReservations.Contracts;

namespace InventoryService.Application.StockReservations.Validation;

public sealed class ReserveStockForOrderItemCommandValidator : AbstractValidator<ReserveStockForOrderItemCommand>
{
    public ReserveStockForOrderItemCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();

        RuleFor(command => command.Quantity)
            .GreaterThan(0);
    }
}