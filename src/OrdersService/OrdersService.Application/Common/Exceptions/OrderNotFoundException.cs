namespace OrdersService.Application.Common.Exceptions;

public sealed class OrderNotFoundException(Guid orderId) 
    : Exception($"Order with ID '{orderId}' was not found.")
{
    public Guid OrderId { get; } = orderId;
}