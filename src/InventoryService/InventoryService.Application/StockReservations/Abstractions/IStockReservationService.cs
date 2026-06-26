using InventoryService.Application.StockReservations.Contracts;

namespace InventoryService.Application.StockReservations.Abstractions;

public interface IStockReservationService
{
    Task HandleOrderCreatedAsync(
        ReserveStockForOrderCommand command,
        CancellationToken cancellationToken);
}