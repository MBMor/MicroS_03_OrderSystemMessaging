using FluentValidation;
using OrdersService.Application.Orders.Contracts;
using OrdersService.Domain.Orders;

namespace OrdersService.Application.Orders.Validation;

public sealed class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(item => item.ProductId)
            .NotEmpty();

        RuleFor(item => item.ProductName)
            .NotEmpty()
            .MaximumLength(OrderItem.ProductNameMaxLength);

        RuleFor(item => item.Quantity)
            .GreaterThan(0);
    }
}