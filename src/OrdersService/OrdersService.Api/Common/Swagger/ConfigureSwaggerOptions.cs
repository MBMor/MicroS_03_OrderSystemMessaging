using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrdersService.Api.Common.Swagger;

public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    : IConfigureOptions<SwaggerGenOptions>
{
    private const string BearerSecuritySchemeName = "bearer";

    private readonly IApiVersionDescriptionProvider _apiVersionDescriptionProvider = apiVersionDescriptionProvider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _apiVersionDescriptionProvider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(
                description.GroupName,
                CreateOpenApiInfo(description));
        }

        AddJwtBearerSecurity(options);
    }

    private static void AddJwtBearerSecurity(SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(BearerSecuritySchemeName, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "JWT Authorization header using the Bearer scheme. Enter only the access token value. Swagger UI will add the Bearer prefix automatically.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSecuritySchemeName, document)] = []
        });
    }

    private static OpenApiInfo CreateOpenApiInfo(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "Orders Service API",
            Version = description.ApiVersion.ToString(),
            Description = "HTTP API for creating and reading orders. Order processing continues asynchronously through RabbitMQ events. Protected endpoints require a Keycloak JWT access token."
        };

        if (description.IsDeprecated)
        {
            info.Description += " This API version is deprecated.";
        }

        return info;
    }
}