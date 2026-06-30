using NotificationsService.Domain.Common;
using NotificationsService.Domain.Notifications;

namespace NotificationsService.Domain.UnitTests.Notifications;

public sealed class NotificationTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WithValidData_CreatesNotificationInCreatedStatus()
    {
        var id = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();

        var notification = new Notification(
            id: id,
            sourceEventId: sourceEventId,
            sourceEventType: "OrderCreated",
            recipient: "john.doe@example.com",
            subject: "Order was created",
            body: "Your order was created.",
            createdAtUtc: UtcNow);

        Assert.Equal(id, notification.Id);
        Assert.Equal(sourceEventId, notification.SourceEventId);
        Assert.Equal("OrderCreated", notification.SourceEventType);
        Assert.Equal("john.doe@example.com", notification.Recipient);
        Assert.Equal("Order was created", notification.Subject);
        Assert.Equal("Your order was created.", notification.Body);
        Assert.Equal(NotificationStatus.Created, notification.Status);
        Assert.Equal(UtcNow, notification.CreatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidSourceEventType_ThrowsDomainException(string sourceEventType)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                sourceEventId: Guid.NewGuid(),
                sourceEventType: sourceEventType,
                recipient: "john.doe@example.com",
                subject: "Order was created",
                body: "Your order was created.",
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidRecipient_ThrowsDomainException(string recipient)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                sourceEventId: Guid.NewGuid(),
                sourceEventType: "OrderCreated",
                recipient: recipient,
                subject: "Order was created",
                body: "Your order was created.",
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidSubject_ThrowsDomainException(string subject)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                sourceEventId: Guid.NewGuid(),
                sourceEventType: "OrderCreated",
                recipient: "john.doe@example.com",
                subject: subject,
                body: "Your order was created.",
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithInvalidBody_ThrowsDomainException(string body)
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                sourceEventId: Guid.NewGuid(),
                sourceEventType: "OrderCreated",
                recipient: "john.doe@example.com",
                subject: "Order was created",
                body: body,
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptySourceEventId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new Notification(
                id: Guid.NewGuid(),
                sourceEventId: Guid.Empty,
                sourceEventType: "OrderCreated",
                recipient: "john.doe@example.com",
                subject: "Order was created",
                body: "Your order was created.",
                createdAtUtc: UtcNow));

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void MarkProcessed_WhenNotificationIsCreated_ChangesStatusToProcessed()
    {
        var notification = CreateValidNotification();

        notification.MarkProcessed();

        Assert.Equal(NotificationStatus.Processed, notification.Status);
    }

    [Fact]
    public void MarkFailed_WhenNotificationIsCreated_ChangesStatusToFailed()
    {
        var notification = CreateValidNotification();

        notification.MarkFailed();

        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Fact]
    public void MarkProcessed_WhenNotificationIsFailed_ThrowsInvalidNotificationStatusTransitionException()
    {
        var notification = CreateValidNotification();

        notification.MarkFailed();

        var exception = Assert.Throws<InvalidNotificationStatusTransitionException>(() =>
            notification.MarkProcessed());

        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public void MarkFailed_WhenNotificationIsProcessed_ThrowsInvalidNotificationStatusTransitionException()
    {
        var notification = CreateValidNotification();

        notification.MarkProcessed();

        var exception = Assert.Throws<InvalidNotificationStatusTransitionException>(() =>
            notification.MarkFailed());

        Assert.NotEmpty(exception.Message);
    }

    private static Notification CreateValidNotification()
    {
        return new Notification(
            id: Guid.NewGuid(),
            sourceEventId: Guid.NewGuid(),
            sourceEventType: "OrderCreated",
            recipient: "john.doe@example.com",
            subject: "Order was created",
            body: "Your order was created.",
            createdAtUtc: UtcNow);
    }
}