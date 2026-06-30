using InventoryService.Domain.Common;
using InventoryService.Domain.StockReservations;

namespace InventoryService.Domain.UnitTests.StockReservations;

public sealed class StockReservationTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateReserved_WithValidData_CreatesReservedStockReservation()
    {
        var reservationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var reservationItem = new StockReservationItem(
            id: Guid.NewGuid(),
            stockReservationId: reservationId,
            productId: Guid.NewGuid(),
            quantity: 2);

        var reservation = StockReservation.CreateReserved(
            id: reservationId,
            orderId: orderId,
            items: [reservationItem],
            createdAtUtc: UtcNow);

        Assert.Equal(reservationId, reservation.Id);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
        Assert.Null(reservation.FailureReason);
        Assert.Equal(UtcNow, reservation.CreatedAtUtc);
        Assert.Single(reservation.Items);
    }

    [Fact]
    public void CreateFailed_WithValidData_CreatesFailedStockReservation()
    {
        var reservationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var reservationItem = new StockReservationItem(
            id: Guid.NewGuid(),
            stockReservationId: reservationId,
            productId: Guid.NewGuid(),
            quantity: 999);

        var reservation = StockReservation.CreateFailed(
            id: reservationId,
            orderId: orderId,
            failureReason: "Insufficient stock.",
            items: [reservationItem],
            createdAtUtc: UtcNow);

        Assert.Equal(reservationId, reservation.Id);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(StockReservationStatus.Failed, reservation.Status);
        Assert.Equal("Insufficient stock.", reservation.FailureReason);
        Assert.Equal(UtcNow, reservation.CreatedAtUtc);
        Assert.Single(reservation.Items);
    }

    [Fact]
    public void CreateReserved_WithoutItems_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            StockReservation.CreateReserved(
                id: Guid.NewGuid(),
                orderId: Guid.NewGuid(),
                items: [],
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateFailed_WithInvalidFailureReason_ThrowsDomainException(string failureReason)
    {
        var reservationId = Guid.NewGuid();

        var reservationItem = new StockReservationItem(
            id: Guid.NewGuid(),
            stockReservationId: reservationId,
            productId: Guid.NewGuid(),
            quantity: 999);

        var exception = Assert.Throws<DomainException>(() =>
            StockReservation.CreateFailed(
                id: reservationId,
                orderId: Guid.NewGuid(),
                failureReason: failureReason,
                items: [reservationItem],
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }
}