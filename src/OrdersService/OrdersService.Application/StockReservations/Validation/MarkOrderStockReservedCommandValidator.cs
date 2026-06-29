using FluentValidation;
using OrdersService.Application.StockReservations.Contracts;

namespace OrdersService.Application.StockReservations.Validation;

public sealed class MarkOrderStockReservedCommandValidator : AbstractValidator<MarkOrderStockReservedCommand>
{
    public MarkOrderStockReservedCommandValidator()
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
    }
}