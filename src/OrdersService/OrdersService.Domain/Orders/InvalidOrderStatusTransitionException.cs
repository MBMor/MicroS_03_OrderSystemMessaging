using OrdersService.Domain.Common;

namespace OrdersService.Domain.Orders;

public sealed class InvalidOrderStatusTransitionException(OrderStatus currentStatus, OrderStatus requestedStatus) 
    : DomainException($"Cannot change order status from '{currentStatus}' to '{requestedStatus}'.")
{
    public OrderStatus CurrentStatus { get; } = currentStatus;

    public OrderStatus RequestedStatus { get; } = requestedStatus;
}