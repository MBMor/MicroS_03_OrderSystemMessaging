namespace OrdersService.Domain.Orders;

public enum OrderStatus
{
    PendingStockReservation = 1,
    StockReserved = 2,
    StockReservationFailed = 3,
    Cancelled = 4
}