using InventoryService.Domain.StockReservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public sealed class StockReservationItemConfiguration : IEntityTypeConfiguration<StockReservationItem>
{
    public void Configure(EntityTypeBuilder<StockReservationItem> builder)
    {
        builder.ToTable("StockReservationItems");

        builder.HasKey(stockReservationItem => stockReservationItem.Id);

        builder.Property(stockReservationItem => stockReservationItem.StockReservationId)
            .IsRequired();

        builder.Property(stockReservationItem => stockReservationItem.ProductId)
            .IsRequired();

        builder.Property(stockReservationItem => stockReservationItem.Quantity)
            .IsRequired();

        builder.HasIndex(stockReservationItem => stockReservationItem.StockReservationId);

        builder.HasIndex(stockReservationItem => stockReservationItem.ProductId);

        builder.HasIndex(stockReservationItem => new
        {
            stockReservationItem.StockReservationId,
            stockReservationItem.ProductId
        }).IsUnique();
    }
}