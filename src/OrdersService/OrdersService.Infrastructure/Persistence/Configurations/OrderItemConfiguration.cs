using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrdersService.Domain.Orders;

namespace OrdersService.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(orderItem => orderItem.Id);

        builder.Property(orderItem => orderItem.OrderId)
            .IsRequired();

        builder.Property(orderItem => orderItem.ProductId)
            .IsRequired();

        builder.Property(orderItem => orderItem.ProductName)
            .HasMaxLength(OrderItem.ProductNameMaxLength)
            .IsRequired();

        builder.Property(orderItem => orderItem.Quantity)
            .IsRequired();

        builder.HasIndex(orderItem => orderItem.OrderId);

        builder.HasIndex(orderItem => orderItem.ProductId);
    }
}