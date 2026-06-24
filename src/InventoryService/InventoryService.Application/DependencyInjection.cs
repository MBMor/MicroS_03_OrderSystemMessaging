using FluentValidation;
using InventoryService.Application.InventoryItems.Contracts;
using InventoryService.Application.InventoryItems.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateInventoryItemRequest>, CreateInventoryItemRequestValidator>();
        services.AddScoped<IValidator<UpdateInventoryItemRequest>, UpdateInventoryItemRequestValidator>();
        services.AddScoped<IValidator<ListInventoryItemsRequest>, ListInventoryItemsRequestValidator>();

        return services;
    }
}