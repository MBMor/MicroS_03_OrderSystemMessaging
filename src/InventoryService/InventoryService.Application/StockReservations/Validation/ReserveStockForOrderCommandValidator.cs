using FluentValidation;
using InventoryService.Application.StockReservations.Contracts;

namespace InventoryService.Application.StockReservations.Validation;

public sealed class ReserveStockForOrderCommandValidator : AbstractValidator<ReserveStockForOrderCommand>
{
    public ReserveStockForOrderCommandValidator()
    {
        RuleFor(command => command.MessageId)
            .NotEmpty();

        RuleFor(command => command.EventType)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.CorrelationId)
            .NotEmpty();

        RuleFor(command => command.OrderId)
            .NotEmpty();

        RuleFor(command => command.CustomerName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.CustomerEmail)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress();

        RuleFor(command => command.Items)
            .NotNull()
            .Must(items => items is { Count: > 0 })
            .WithMessage("At least one order item is required.");

        RuleForEach(command => command.Items)
            .SetValidator(new ReserveStockForOrderItemCommandValidator());
    }
}