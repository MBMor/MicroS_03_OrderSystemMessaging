using OrdersService.Domain.Common;
using OrdersService.Domain.Orders;

namespace OrdersService.Domain.UnitTests.Orders;

public sealed class OrderTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WithValidData_CreatesOrderInPendingStockReservationStatus()
    {
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: productId,
            productName: "Keyboard",
            quantity: 2);

        var order = new Order(
            id: orderId,
            customerName: "John Doe",
            customerEmail: "john.doe@example.com",
            items: [item],
            createdAtUtc: UtcNow);

        Assert.Equal(orderId, order.Id);
        Assert.Equal("John Doe", order.CustomerName);
        Assert.Equal("john.doe@example.com", order.CustomerEmail);
        Assert.Equal(OrderStatus.PendingStockReservation, order.Status);
        Assert.Equal(UtcNow, order.CreatedAtUtc);
        Assert.Equal(UtcNow, order.UpdatedAtUtc);
        Assert.Single(order.Items);
    }

    [Fact]
    public void Constructor_WithoutItems_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Order(
                id: Guid.NewGuid(),
                customerName: "John Doe",
                customerEmail: "john.doe@example.com",
                items: [],
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidCustomerName_ThrowsDomainException(string customerName)
    {
        var orderId = Guid.NewGuid();

        var item = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: Guid.NewGuid(),
            productName: "Keyboard",
            quantity: 2);

        var exception = Assert.Throws<DomainException>(() =>
            new Order(
                id: orderId,
                customerName: customerName,
                customerEmail: "john.doe@example.com",
                items: [item],
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    public void Constructor_WithInvalidCustomerEmail_ThrowsDomainException(string customerEmail)
    {
        var orderId = Guid.NewGuid();

        var item = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: Guid.NewGuid(),
            productName: "Keyboard",
            quantity: 2);

        var exception = Assert.Throws<DomainException>(() =>
            new Order(
                id: orderId,
                customerName: "John Doe",
                customerEmail: customerEmail,
                items: [item],
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void MarkStockReserved_WhenOrderIsPending_ChangesStatusToStockReserved()
    {
        var order = CreateValidOrder();

        var updatedAtUtc = UtcNow.AddMinutes(5);

        order.MarkStockReserved(updatedAtUtc);

        Assert.Equal(OrderStatus.StockReserved, order.Status);
        Assert.Equal(updatedAtUtc, order.UpdatedAtUtc);
    }

    [Fact]
    public void MarkStockReservationFailed_WhenOrderIsPending_ChangesStatusToStockReservationFailed()
    {
        var order = CreateValidOrder();

        var updatedAtUtc = UtcNow.AddMinutes(5);

        order.MarkStockReservationFailed(updatedAtUtc);

        Assert.Equal(OrderStatus.StockReservationFailed, order.Status);
        Assert.Equal(updatedAtUtc, order.UpdatedAtUtc);
    }

    [Fact]
    public void Cancel_WhenOrderIsPending_ChangesStatusToCancelled()
    {
        var order = CreateValidOrder();

        var updatedAtUtc = UtcNow.AddMinutes(5);

        order.Cancel(updatedAtUtc);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(updatedAtUtc, order.UpdatedAtUtc);
    }

    [Fact]
    public void MarkStockReserved_WhenOrderIsCancelled_ThrowsInvalidOrderStatusTransitionException()
    {
        var order = CreateValidOrder();

        order.Cancel(UtcNow.AddMinutes(1));

        var exception = Assert.Throws<InvalidOrderStatusTransitionException>(() =>
            order.MarkStockReserved(UtcNow.AddMinutes(2)));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void MarkStockReservationFailed_WhenOrderIsStockReserved_ThrowsInvalidOrderStatusTransitionException()
    {
        var order = CreateValidOrder();

        order.MarkStockReserved(UtcNow.AddMinutes(1));

        var exception = Assert.Throws<InvalidOrderStatusTransitionException>(() =>
            order.MarkStockReservationFailed(UtcNow.AddMinutes(2)));

        Assert.NotEmpty(exception.Message);
    }

    private static Order CreateValidOrder()
    {
        var orderId = Guid.NewGuid();

        var item = new OrderItem(
            id: Guid.NewGuid(),
            orderId: orderId,
            productId: Guid.NewGuid(),
            productName: "Keyboard",
            quantity: 2);

        return new Order(
            id: orderId,
            customerName: "John Doe",
            customerEmail: "john.doe@example.com",
            items: [item],
            createdAtUtc: UtcNow);
    }
}