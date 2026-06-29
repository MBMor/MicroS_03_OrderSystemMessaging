using FluentValidation;
using NotificationsService.Application.EventNotifications.Contracts;

namespace NotificationsService.Application.EventNotifications.Validation;

public sealed class CreateStockReservedNotificationCommandValidator
    : AbstractValidator<CreateStockReservedNotificationCommand>
{
    public CreateStockReservedNotificationCommandValidator()
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
    }
}