using InventoryService.Domain.Common;
using InventoryService.Domain.Inventory;

namespace InventoryService.Domain.UnitTests.Inventory;

public sealed class InventoryItemTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WithValidData_CreatesInventoryItem()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var item = new InventoryItem(
            id: id,
            productId: productId,
            productName: "Keyboard",
            availableQuantity: 50,
            createdAtUtc: UtcNow);

        Assert.Equal(id, item.Id);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Keyboard", item.ProductName);
        Assert.Equal(50, item.AvailableQuantity);
        Assert.Equal(0, item.ReservedQuantity);
        Assert.Equal(UtcNow, item.CreatedAtUtc);
        Assert.Equal(UtcNow, item.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidProductName_ThrowsDomainException(string productName)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new InventoryItem(
                id: Guid.NewGuid(),
                productId: Guid.NewGuid(),
                productName: productName,
                availableQuantity: 50,
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void Constructor_WithNegativeAvailableQuantity_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new InventoryItem(
                id: Guid.NewGuid(),
                productId: Guid.NewGuid(),
                productName: "Keyboard",
                availableQuantity: -1,
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void CanReserve_WhenAvailableQuantityIsEnough_ReturnsTrue()
    {
        var item = CreateInventoryItem(availableQuantity: 50);

        var result = item.CanReserve(10);

        Assert.True(result);
    }

    [Fact]
    public void CanReserve_WhenAvailableQuantityIsNotEnough_ReturnsFalse()
    {
        var item = CreateInventoryItem(availableQuantity: 5);

        var result = item.CanReserve(10);

        Assert.False(result);
    }

    [Fact]
    public void Reserve_WhenAvailableQuantityIsEnough_DecreasesAvailableAndIncreasesReserved()
    {
        var item = CreateInventoryItem(availableQuantity: 50);

        var updatedAtUtc = UtcNow.AddMinutes(5);

        item.Reserve(
            quantity: 2,
            updatedAtUtc: updatedAtUtc);

        Assert.Equal(48, item.AvailableQuantity);
        Assert.Equal(2, item.ReservedQuantity);
        Assert.Equal(updatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void Reserve_WhenAvailableQuantityIsNotEnough_ThrowsInvalidStockOperationException()
    {
        var item = CreateInventoryItem(availableQuantity: 1);

        var exception = Assert.Throws<InvalidStockOperationException>(() =>
            item.Reserve(
                quantity: 2,
                updatedAtUtc: UtcNow.AddMinutes(5)));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_WithInvalidQuantity_ThrowsInvalidStockOperationException(int quantity)
    {
        var item = CreateInventoryItem(availableQuantity: 50);

        var exception = Assert.Throws<InvalidStockOperationException>(() =>
            item.Reserve(
                quantity: quantity,
                updatedAtUtc: UtcNow.AddMinutes(5)));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void Update_WithValidData_ChangesProductNameAndAvailableQuantity()
    {
        var item = CreateInventoryItem(availableQuantity: 50);

        var updatedAtUtc = UtcNow.AddMinutes(5);

        item.Update(
            productName: "Updated Keyboard",
            availableQuantity: 75,
            updatedAtUtc: updatedAtUtc);

        Assert.Equal("Updated Keyboard", item.ProductName);
        Assert.Equal(75, item.AvailableQuantity);
        Assert.Equal(updatedAtUtc, item.UpdatedAtUtc);
    }

    private static InventoryItem CreateInventoryItem(int availableQuantity)
    {
        return new InventoryItem(
            id: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            productName: "Keyboard",
            availableQuantity: availableQuantity,
            createdAtUtc: UtcNow);
    }
}