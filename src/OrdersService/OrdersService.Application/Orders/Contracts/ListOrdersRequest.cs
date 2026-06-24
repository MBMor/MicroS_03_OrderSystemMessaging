namespace OrdersService.Application.Orders.Contracts;

public sealed record ListOrdersRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? Status { get; init; }

    public string? SortBy { get; init; } = "createdAtUtc";

    public string? SortDirection { get; init; } = "desc";
}