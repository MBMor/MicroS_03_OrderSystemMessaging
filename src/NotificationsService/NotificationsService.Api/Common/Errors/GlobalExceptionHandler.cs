using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NotificationsService.Domain.Common;

namespace NotificationsService.Api.Common.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = CreateProblemDetails(
            httpContext,
            exception);

        LogException(exception, problemDetails.Status);

        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;

        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => CreateValidationProblemDetails(
                httpContext,
                validationException),

            DomainException domainException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Domain rule violation.",
                Detail = domainException.Message,
                Instance = httpContext.Request.Path
            },

            ArgumentOutOfRangeException argumentOutOfRangeException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request.",
                Detail = argumentOutOfRangeException.Message,
                Instance = httpContext.Request.Path
            },

            ArgumentException argumentException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request.",
                Detail = argumentException.Message,
                Instance = httpContext.Request.Path
            },

            NotImplementedException => new ProblemDetails
            {
                Status = StatusCodes.Status501NotImplemented,
                Title = "Feature is not implemented.",
                Detail = "The requested feature is not implemented yet.",
                Instance = httpContext.Request.Path
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected server error.",
                Detail = "An unexpected error occurred while processing the request.",
                Instance = httpContext.Request.Path
            }
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problemDetails;
    }

    private static ProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ValidationException validationException)
    {
        var errors = validationException.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.ErrorMessage)
                    .ToArray());

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed.",
            Detail = "One or more validation errors occurred.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["errors"] = errors;

        return problemDetails;
    }

    private void LogException(Exception exception, int? statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing the request.");
            return;
        }

        _logger.LogWarning(
            exception,
            "Handled exception occurred while processing the request.");
    }
}