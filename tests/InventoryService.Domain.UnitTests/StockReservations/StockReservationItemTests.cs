using InventoryService.Domain.Common;
using InventoryService.Domain.StockReservations;

namespace InventoryService.Domain.UnitTests.StockReservations;

public sealed class StockReservationItemTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesStockReservationItem()
    {
        var id = Guid.NewGuid();
        var stockReservationId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new StockReservationItem(
            id: id,
            stockReservationId: stockReservationId,
            productId: productId,
            quantity: 2);

        Assert.Equal(id, item.Id);
        Assert.Equal(stockReservationId, item.StockReservationId);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(2, item.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidQuantity_ThrowsDomainException(int quantity)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new StockReservationItem(
                id: Guid.NewGuid(),
                stockReservationId: Guid.NewGuid(),
                productId: Guid.NewGuid(),
                quantity: quantity));

        Assert.NotEmpty(exception.Message);
    }
}