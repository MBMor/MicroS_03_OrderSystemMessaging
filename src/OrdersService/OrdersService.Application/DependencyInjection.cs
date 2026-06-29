using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Application.Orders.Contracts;
using OrdersService.Application.Orders.Validation;
using OrdersService.Application.StockReservations.Contracts;
using OrdersService.Application.StockReservations.Validation;

namespace OrdersService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateOrderItemRequest>, CreateOrderItemRequestValidator>();
        services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
        services.AddScoped<IValidator<ListOrdersRequest>, ListOrdersRequestValidator>();

        services.AddScoped<IValidator<MarkOrderStockReservedCommand>, MarkOrderStockReservedCommandValidator>();
        services.AddScoped<IValidator<MarkOrderStockReservationFailedCommand>, MarkOrderStockReservationFailedCommandValidator>();

        return services;
    }
}