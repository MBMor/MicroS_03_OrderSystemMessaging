using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrdersService.Application.Orders.Contracts;
using OrdersService.Application.Orders.Validation;

namespace OrdersService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateOrderItemRequest>, CreateOrderItemRequestValidator>();
        services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
        services.AddScoped<IValidator<ListOrdersRequest>, ListOrdersRequestValidator>();

        return services;
    }
}