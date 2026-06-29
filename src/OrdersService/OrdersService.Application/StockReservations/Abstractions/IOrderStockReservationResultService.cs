using OrdersService.Application.StockReservations.Contracts;

namespace OrdersService.Application.StockReservations.Abstractions;

public interface IOrderStockReservationResultService
{
    Task HandleStockReservedAsync(
        MarkOrderStockReservedCommand command,
        CancellationToken cancellationToken);

    Task HandleStockReservationFailedAsync(
        MarkOrderStockReservationFailedCommand command,
        CancellationToken cancellationToken);
}