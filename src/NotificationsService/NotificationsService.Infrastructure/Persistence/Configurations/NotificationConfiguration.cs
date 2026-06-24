using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationsService.Domain.Notifications;

namespace NotificationsService.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.SourceEventId)
            .IsRequired();

        builder.Property(notification => notification.SourceEventType)
            .HasMaxLength(Notification.SourceEventTypeMaxLength)
            .IsRequired();

        builder.Property(notification => notification.Recipient)
            .HasMaxLength(Notification.RecipientMaxLength)
            .IsRequired();

        builder.Property(notification => notification.Subject)
            .HasMaxLength(Notification.SubjectMaxLength)
            .IsRequired();

        builder.Property(notification => notification.Body)
            .HasMaxLength(Notification.BodyMaxLength)
            .IsRequired();

        builder.Property(notification => notification.Status)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(notification => notification.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(notification => new
        {
            notification.SourceEventId,
            notification.SourceEventType
        }).IsUnique();

        builder.HasIndex(notification => notification.SourceEventType);

        builder.HasIndex(notification => notification.Status);

        builder.HasIndex(notification => notification.CreatedAtUtc);
    }
}