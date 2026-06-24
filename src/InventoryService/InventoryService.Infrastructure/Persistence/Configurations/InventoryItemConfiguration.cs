using InventoryService.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryService.Infrastructure.Persistence.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");

        builder.HasKey(inventoryItem => inventoryItem.Id);

        builder.Property(inventoryItem => inventoryItem.ProductId)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.ProductName)
            .HasMaxLength(InventoryItem.ProductNameMaxLength)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.AvailableQuantity)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.ReservedQuantity)
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(inventoryItem => inventoryItem.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(inventoryItem => inventoryItem.ProductId)
            .IsUnique();

        builder.HasIndex(inventoryItem => inventoryItem.ProductName);
    }
}