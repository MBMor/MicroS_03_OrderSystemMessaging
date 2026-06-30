using OrdersService.Domain.Common;
using OrdersService.Domain.Orders;

namespace OrdersService.Domain.UnitTests.Orders;

public sealed class OrderItemTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesOrderItem()
    {
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new OrderItem(
            id: id,
            orderId: orderId,
            productId: productId,
            productName: "Keyboard",
            quantity: 2);

        Assert.Equal(id, item.Id);
        Assert.Equal(orderId, item.OrderId);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Keyboard", item.ProductName);
        Assert.Equal(2, item.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidQuantity_ThrowsDomainException(int quantity)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new OrderItem(
                id: Guid.NewGuid(),
                orderId: Guid.NewGuid(),
                productId: Guid.NewGuid(),
                productName: "Keyboard",
                quantity: quantity));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidProductName_ThrowsDomainException(string productName)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new OrderItem(
                id: Guid.NewGuid(),
                orderId: Guid.NewGuid(),
                productId: Guid.NewGuid(),
                productName: productName,
                quantity: 1));

        Assert.NotEmpty(exception.Message);
    }
}