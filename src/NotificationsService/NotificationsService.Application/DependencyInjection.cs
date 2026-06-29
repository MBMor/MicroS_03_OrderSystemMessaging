using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NotificationsService.Application.EventNotifications.Contracts;
using NotificationsService.Application.EventNotifications.Validation;

namespace NotificationsService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateOrderCreatedNotificationCommand>, CreateOrderCreatedNotificationCommandValidator>();
        services.AddScoped<IValidator<CreateStockReservedNotificationCommand>, CreateStockReservedNotificationCommandValidator>();
        services.AddScoped<IValidator<CreateStockReservationFailedNotificationCommand>, CreateStockReservationFailedNotificationCommandValidator>();

        return services;
    }
}