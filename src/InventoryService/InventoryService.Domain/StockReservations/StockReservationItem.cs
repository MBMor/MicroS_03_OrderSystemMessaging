using InventoryService.Domain.Common;

namespace InventoryService.Domain.StockReservations;

public sealed class StockReservationItem
{
    private StockReservationItem()
    {
    }

    public StockReservationItem(
        Guid id,
        Guid stockReservationId,
        Guid productId,
        int quantity)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Stock reservation item ID is required.");
        }

        if (stockReservationId == Guid.Empty)
        {
            throw new DomainException("Stock reservation ID is required.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("Product ID is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Reservation quantity must be greater than 0.");
        }

        Id = id;
        StockReservationId = stockReservationId;
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid StockReservationId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }
}