using System.Net.Mail;
using OrdersService.Domain.Common;

namespace OrdersService.Domain.Orders;

public sealed class Order
{
    public const int CustomerNameMaxLength = 200;
    public const int CustomerEmailMaxLength = 320;

    private readonly List<OrderItem> _items = [];

    private Order()
    {
        CustomerName = string.Empty;
        CustomerEmail = string.Empty;
    }

    public Order(
        Guid id,
        string customerName,
        string customerEmail,
        IEnumerable<OrderItem> items,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Order ID is required.");
        }

        SetCustomer(customerName, customerEmail);

        var orderItems = items?.ToList() ?? throw new DomainException("Order items are required.");

        if (orderItems.Count == 0)
        {
            throw new DomainException("Order must contain at least one item.");
        }

        if (orderItems.Any(item => item.OrderId != id))
        {
            throw new DomainException("All order items must belong to the order.");
        }

        Id = id;
        Status = OrderStatus.PendingStockReservation;
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;

        _items.AddRange(orderItems);
    }

    public Guid Id { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public void MarkStockReserved(DateTime updatedAtUtc)
    {
        ChangeStatus(OrderStatus.StockReserved, updatedAtUtc);
    }

    public void MarkStockReservationFailed(DateTime updatedAtUtc)
    {
        ChangeStatus(OrderStatus.StockReservationFailed, updatedAtUtc);
    }

    public void Cancel(DateTime updatedAtUtc)
    {
        ChangeStatus(OrderStatus.Cancelled, updatedAtUtc);
    }

    private void ChangeStatus(OrderStatus requestedStatus, DateTime updatedAtUtc)
    {
        if (!CanChangeStatusTo(requestedStatus))
        {
            throw new InvalidOrderStatusTransitionException(Status, requestedStatus);
        }

        Status = requestedStatus;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc);
    }

    private bool CanChangeStatusTo(OrderStatus requestedStatus)
    {
        return Status switch
        {
            OrderStatus.PendingStockReservation => requestedStatus is
                OrderStatus.StockReserved or
                OrderStatus.StockReservationFailed or
                OrderStatus.Cancelled,

            _ => false
        };
    }

    private void SetCustomer(string customerName, string customerEmail)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new DomainException("Customer name is required.");
        }

        if (customerName.Length > CustomerNameMaxLength)
        {
            throw new DomainException($"Customer name must not exceed {CustomerNameMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            throw new DomainException("Customer e-mail is required.");
        }

        if (customerEmail.Length > CustomerEmailMaxLength)
        {
            throw new DomainException($"Customer e-mail must not exceed {CustomerEmailMaxLength} characters.");
        }

        if (!IsValidEmail(customerEmail))
        {
            throw new DomainException("Customer e-mail must be a valid e-mail address.");
        }

        CustomerName = customerName.Trim();
        CustomerEmail = customerEmail.Trim();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var mailAddress = new MailAddress(email);
            return string.Equals(mailAddress.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
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