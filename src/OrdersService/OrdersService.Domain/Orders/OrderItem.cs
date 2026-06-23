using OrdersService.Domain.Common;

namespace OrdersService.Domain.Orders;

public sealed class OrderItem
{
    public const int ProductNameMaxLength = 200;

    private OrderItem()
    {
        ProductName = string.Empty;
    }

    public OrderItem(
        Guid id,
        Guid orderId,
        Guid productId,
        string productName,
        int quantity)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Order item ID is required.");
        }

        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order ID is required.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("Product ID is required.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("Product name is required.");
        }

        if (productName.Length > ProductNameMaxLength)
        {
            throw new DomainException($"Product name must not exceed {ProductNameMaxLength} characters.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than 0.");
        }

        Id = id;
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName.Trim();
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; }

    public int Quantity { get; private set; }
}