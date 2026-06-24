using Microsoft.EntityFrameworkCore;
using NotificationsService.Application.Common.Pagination;
using NotificationsService.Application.Notifications.Abstractions;
using NotificationsService.Application.Notifications.Contracts;
using NotificationsService.Domain.Notifications;
using NotificationsService.Infrastructure.Persistence;

namespace NotificationsService.Infrastructure.Notifications;

public sealed class NotificationsApplicationService(NotificationsDbContext dbContext) : INotificationsService
{
    private readonly NotificationsDbContext _dbContext = dbContext;

    public async Task<NotificationResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var notification = await _dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(
                notification => notification.Id == id,
                cancellationToken);

        return notification is null
            ? null
            : MapToResponse(notification);
    }

    public async Task<PagedResult<NotificationResponse>> ListAsync(
        ListNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateListRequest(request);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SourceEventType))
        {
            var sourceEventType = request.SourceEventType.Trim();

            query = query.Where(notification =>
                notification.SourceEventType == sourceEventType);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = Enum.Parse<NotificationStatus>(
                request.Status,
                ignoreCase: true);

            query = query.Where(notification => notification.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySorting(
            query,
            request.SortBy!,
            request.SortDirection!);

        var notifications = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = notifications
            .Select(MapToResponse)
            .ToList();

        return new PagedResult<NotificationResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    private static void ValidateListRequest(ListNotificationsRequest request)
    {
        if (request.Page <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Page),
                "Page must be greater than 0.");
        }

        if (request.PageSize is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PageSize),
                "PageSize must be between 1 and 100.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !Enum.TryParse<NotificationStatus>(
                request.Status,
                ignoreCase: true,
                out _))
        {
            throw new ArgumentException(
                "Status must be a valid notification status.",
                nameof(request.Status));
        }

        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            throw new ArgumentException(
                "SortBy is required.",
                nameof(request.SortBy));
        }

        if (string.IsNullOrWhiteSpace(request.SortDirection))
        {
            throw new ArgumentException(
                "SortDirection is required.",
                nameof(request.SortDirection));
        }

        if (!IsAllowedSortBy(request.SortBy))
        {
            throw new ArgumentException(
                "SortBy must be one of: createdAtUtc, sourceEventType, recipient, status.",
                nameof(request.SortBy));
        }

        if (!IsAllowedSortDirection(request.SortDirection))
        {
            throw new ArgumentException(
                "SortDirection must be either 'asc' or 'desc'.",
                nameof(request.SortDirection));
        }
    }

    private static bool IsAllowedSortBy(string sortBy)
    {
        return sortBy.Equals("createdAtUtc", StringComparison.OrdinalIgnoreCase)
            || sortBy.Equals("sourceEventType", StringComparison.OrdinalIgnoreCase)
            || sortBy.Equals("recipient", StringComparison.OrdinalIgnoreCase)
            || sortBy.Equals("status", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedSortDirection(string sortDirection)
    {
        return sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            || sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
    }

    private static IQueryable<Notification> ApplySorting(
        IQueryable<Notification> query,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(
            sortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "createdatutc" => descending
                ? query.OrderByDescending(notification => notification.CreatedAtUtc)
                : query.OrderBy(notification => notification.CreatedAtUtc),

            "sourceeventtype" => descending
                ? query.OrderByDescending(notification => notification.SourceEventType)
                : query.OrderBy(notification => notification.SourceEventType),

            "recipient" => descending
                ? query.OrderByDescending(notification => notification.Recipient)
                : query.OrderBy(notification => notification.Recipient),

            "status" => descending
                ? query.OrderByDescending(notification => notification.Status)
                : query.OrderBy(notification => notification.Status),

            _ => descending
                ? query.OrderByDescending(notification => notification.CreatedAtUtc)
                : query.OrderBy(notification => notification.CreatedAtUtc)
        };
    }

    private static NotificationResponse MapToResponse(Notification notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            SourceEventId = notification.SourceEventId,
            SourceEventType = notification.SourceEventType,
            Recipient = notification.Recipient,
            Subject = notification.Subject,
            Body = notification.Body,
            Status = notification.Status.ToString(),
            CreatedAtUtc = notification.CreatedAtUtc
        };
    }
}