using InventoryService.Domain.Common;

namespace InventoryService.Domain.StockReservations;

public sealed class StockReservation
{
    public const int FailureReasonMaxLength = 500;

    private readonly List<StockReservationItem> _items = [];

    private StockReservation()
    {
    }

    private StockReservation(
        Guid id,
        Guid orderId,
        StockReservationStatus status,
        string? failureReason,
        IEnumerable<StockReservationItem> items,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Stock reservation ID is required.");
        }

        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order ID is required.");
        }

        var reservationItems = items?.ToList() ?? throw new DomainException("Stock reservation items are required.");

        if (reservationItems.Count == 0)
        {
            throw new DomainException("Stock reservation must contain at least one item.");
        }

        if (reservationItems.Any(item => item.StockReservationId != id))
        {
            throw new DomainException("All stock reservation items must belong to the reservation.");
        }

        if (status == StockReservationStatus.Failed && string.IsNullOrWhiteSpace(failureReason))
        {
            throw new DomainException("Failure reason is required for failed stock reservation.");
        }

        if (failureReason is not null && failureReason.Length > FailureReasonMaxLength)
        {
            throw new DomainException($"Failure reason must not exceed {FailureReasonMaxLength} characters.");
        }

        Id = id;
        OrderId = orderId;
        Status = status;
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;

        _items.AddRange(reservationItems);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public StockReservationStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<StockReservationItem> Items => _items.AsReadOnly();

    public static StockReservation CreateReserved(
        Guid id,
        Guid orderId,
        IEnumerable<StockReservationItem> items,
        DateTime createdAtUtc)
    {
        return new StockReservation(
            id,
            orderId,
            StockReservationStatus.Reserved,
            failureReason: null,
            items,
            createdAtUtc);
    }

    public static StockReservation CreateFailed(
        Guid id,
        Guid orderId,
        string failureReason,
        IEnumerable<StockReservationItem> items,
        DateTime createdAtUtc)
    {
        return new StockReservation(
            id,
            orderId,
            StockReservationStatus.Failed,
            failureReason,
            items,
            createdAtUtc);
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