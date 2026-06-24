using FluentValidation;
using OrdersService.Application.Orders.Contracts;
using OrdersService.Domain.Orders;

namespace OrdersService.Application.Orders.Validation;

public sealed class ListOrdersRequestValidator : AbstractValidator<ListOrdersRequest>
{
    private static readonly HashSet<string> AllowedSortByValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAtUtc",
        "updatedAtUtc",
        "customerName",
        "status"
    };

    private static readonly HashSet<string> AllowedSortDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc",
        "desc"
    };

    public ListOrdersRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThan(0);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(request => request.Status)
            .Must(BeValidOrderStatus)
            .When(request => !string.IsNullOrWhiteSpace(request.Status))
            .WithMessage("Status must be a valid order status.");

        RuleFor(request => request.SortBy)
            .Must(sortBy => !string.IsNullOrWhiteSpace(sortBy) && AllowedSortByValues.Contains(sortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", AllowedSortByValues)}.");

        RuleFor(request => request.SortDirection)
            .Must(sortDirection => !string.IsNullOrWhiteSpace(sortDirection) && AllowedSortDirections.Contains(sortDirection))
            .WithMessage("SortDirection must be either 'asc' or 'desc'.");
    }

    private static bool BeValidOrderStatus(string? status)
    {
        return Enum.TryParse<OrderStatus>(
            status,
            ignoreCase: true,
            out _);
    }
}