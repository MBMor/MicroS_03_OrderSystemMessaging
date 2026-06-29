namespace OrdersService.Infrastructure.Messaging;

public sealed class StockReservationResultConsumerOptions
{
    public const string SectionName = "StockReservationResultConsumers";

    public ushort PrefetchCount { get; init; } = 10;

    public int ConnectionRetryDelaySeconds { get; init; } = 5;
}