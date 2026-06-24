using OrdersService.Application.Common.Pagination;
using OrdersService.Application.Orders.Contracts;

namespace OrdersService.Application.Orders.Abstractions;

public interface IOrdersService
{
    Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken);

    Task<OrderResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<PagedResult<OrderResponse>> ListAsync(
        ListOrdersRequest request,
        CancellationToken cancellationToken);
}