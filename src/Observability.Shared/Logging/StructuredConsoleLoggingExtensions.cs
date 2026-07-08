using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Observability.Shared.Logging;

public static class StructuredConsoleLoggingExtensions
{
    public static ILoggingBuilder UseStructuredJsonConsole(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();

        logging.SetMinimumLevel(LogLevel.Information);

        logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
        logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        logging.AddFilter("Yarp.ReverseProxy", LogLevel.Warning);

        logging.AddFilter("ApiGateway", LogLevel.Information);
        logging.AddFilter("OrdersService", LogLevel.Information);
        logging.AddFilter("InventoryService", LogLevel.Information);
        logging.AddFilter("NotificationsService", LogLevel.Information);
        logging.AddFilter("Observability.Shared", LogLevel.Information);

        logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            options.JsonWriterOptions = new JsonWriterOptions
            {
                Indented = false
            };
        });

        return logging;
    }
}