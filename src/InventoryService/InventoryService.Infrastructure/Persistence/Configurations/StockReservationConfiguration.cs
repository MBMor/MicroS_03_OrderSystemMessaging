using InventoryService.Domain.StockReservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("StockReservations");

        builder.HasKey(stockReservation => stockReservation.Id);

        builder.Property(stockReservation => stockReservation.OrderId)
            .IsRequired();

        builder.Property(stockReservation => stockReservation.Status)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(stockReservation => stockReservation.FailureReason)
            .HasMaxLength(StockReservation.FailureReasonMaxLength);

        builder.Property(stockReservation => stockReservation.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(stockReservation => stockReservation.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasMany(stockReservation => stockReservation.Items)
            .WithOne()
            .HasForeignKey(stockReservationItem => stockReservationItem.StockReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(StockReservation.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(stockReservation => stockReservation.OrderId)
            .IsUnique();

        builder.HasIndex(stockReservation => stockReservation.Status);

        builder.HasIndex(stockReservation => stockReservation.CreatedAtUtc);
    }
}