using Microsoft.EntityFrameworkCore;
using NotificationsService.Domain.Notifications;
using NotificationsService.Infrastructure.Idempotency;

namespace NotificationsService.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}