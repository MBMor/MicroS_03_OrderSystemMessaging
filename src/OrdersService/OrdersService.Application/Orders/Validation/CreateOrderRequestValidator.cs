using FluentValidation;
using OrdersService.Application.Orders.Contracts;
using OrdersService.Domain.Orders;

namespace OrdersService.Application.Orders.Validation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.CustomerName)
            .NotEmpty()
            .MaximumLength(Order.CustomerNameMaxLength);

        RuleFor(request => request.CustomerEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(Order.CustomerEmailMaxLength);

        RuleFor(request => request.Items)
            .NotNull()
            .Must(items => items is not null && items.Count > 0)
            .WithMessage("Order must contain at least one item.");

        RuleForEach(request => request.Items)
            .SetValidator(new CreateOrderItemRequestValidator());
    }
}