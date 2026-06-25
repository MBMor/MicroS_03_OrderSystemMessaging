using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InventoryService.Api.Common.Health;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static Task WriteAsync(
        HttpContext httpContext,
        HealthReport healthReport)
    {
        httpContext.Response.ContentType = "application/json";

        var response = new
        {
            status = healthReport.Status.ToString(),
            totalDuration = healthReport.TotalDuration.TotalMilliseconds,
            checks = healthReport.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds,
                error = entry.Value.Exception?.Message,
                tags = entry.Value.Tags
            })
        };

        return httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonSerializerOptions));
    }
}