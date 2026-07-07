using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Observability.Shared.Logging;

public static class StructuredConsoleLoggingExtensions
{
    public static ILoggingBuilder UseStructuredJsonConsole(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();

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