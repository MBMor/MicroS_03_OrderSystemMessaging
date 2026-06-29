using FluentValidation;
using OrdersService.Application.StockReservations.Contracts;

namespace OrdersService.Application.StockReservations.Validation;

public sealed class MarkOrderStockReservationFailedCommandValidator : AbstractValidator<MarkOrderStockReservationFailedCommand>
{
    public MarkOrderStockReservationFailedCommandValidator()
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

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}