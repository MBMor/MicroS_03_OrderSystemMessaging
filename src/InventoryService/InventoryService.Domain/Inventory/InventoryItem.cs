using InventoryService.Domain.Common;

namespace InventoryService.Domain.Inventory;

public sealed class InventoryItem
{
    public const int ProductNameMaxLength = 200;

    private InventoryItem()
    {
        ProductName = string.Empty;
    }

    public InventoryItem(
        Guid id,
        Guid productId,
        string productName,
        int availableQuantity,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Inventory item ID is required.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("Product ID is required.");
        }

        ProductName = ValidateProductName(productName);

        if (availableQuantity < 0)
        {
            throw new DomainException("Available quantity must be greater than or equal to 0.");
        }

        Id = id;
        ProductId = productId;
        AvailableQuantity = availableQuantity;
        ReservedQuantity = 0;
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; }

    public int AvailableQuantity { get; private set; }

    public int ReservedQuantity { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string productName, int availableQuantity, DateTime updatedAtUtc)
    {
        ProductName = ValidateProductName(productName);

        if (availableQuantity < 0)
        {
            throw new DomainException("Available quantity must be greater than or equal to 0.");
        }

        AvailableQuantity = availableQuantity;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    public bool CanReserve(int quantity)
    {
        if (quantity <= 0)
        {
            return false;
        }

        return AvailableQuantity >= quantity;
    }

    public void Reserve(int quantity, DateTime updatedAtUtc)
    {
        if (quantity <= 0)
        {
            throw new InvalidStockOperationException("Reservation quantity must be greater than 0.");
        }

        if (AvailableQuantity < quantity)
        {
            throw new InvalidStockOperationException(
                $"Insufficient stock for product '{ProductId}'. Requested quantity: {quantity}, available quantity: {AvailableQuantity}.");
        }

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);

        EnsureValidStockState();
    }

    private void EnsureValidStockState()
    {
        if (AvailableQuantity < 0)
        {
            throw new InvalidStockOperationException("Available quantity must not be negative.");
        }

        if (ReservedQuantity < 0)
        {
            throw new InvalidStockOperationException("Reserved quantity must not be negative.");
        }
    }

    private static string ValidateProductName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("Product name is required.");
        }

        if (productName.Length > ProductNameMaxLength)
        {
            throw new DomainException($"Product name must not exceed {ProductNameMaxLength} characters.");
        }

        return productName.Trim();
    }

    private static DateTime EnsureUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            _ => dateTime
        };
    }
}