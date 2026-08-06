using System.Net;
using LanguageLearning.Common.Exceptions;
using LanguageLearning.Common.Results;
using Microsoft.AspNetCore.Diagnostics;

namespace LanguageLearning.WebApi.Middlewares;

/// <summary>
/// Global exception handler that translates known exceptions into structured API responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        return await HandleExceptionAsync(httpContext, exception, cancellationToken);
    }

    private async Task<bool> HandleExceptionAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, problemDetails) = exception switch
        {
            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                CreateProblemDetails(
                    httpContext,
                    HttpStatusCode.NotFound,
                    "Resource Not Found",
                    notFound.Message)),

            BusinessRuleViolationException businessRule => (
                HttpStatusCode.BadRequest,
                CreateProblemDetails(
                    httpContext,
                    HttpStatusCode.BadRequest,
                    "Business Rule Violation",
                    businessRule.Message)),

            UnauthorizedException unauthorized => (
                HttpStatusCode.Unauthorized,
                CreateProblemDetails(
                    httpContext,
                    HttpStatusCode.Unauthorized,
                    "Unauthorized",
                    unauthorized.Message)),

            FluentValidation.ValidationException validation => (
                HttpStatusCode.BadRequest,
                CreateProblemDetails(
                    httpContext,
                    HttpStatusCode.BadRequest,
                    "Validation Failed",
                    validation.Message)),

            _ => (
                HttpStatusCode.InternalServerError,
                CreateProblemDetails(
                    httpContext,
                    HttpStatusCode.InternalServerError,
                    "Internal Server Error",
                    "An unexpected error occurred. Please try again later."))
        };

        if ((int)statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception occurred. {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);
        }

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static object CreateProblemDetails(
        HttpContext httpContext,
        HttpStatusCode status,
        string title,
        string detail)
    {
        return new
        {
            Type = $"https://httpstatuses.com/{(int)status}",
            Title = title,
            Status = (int)status,
            Detail = detail,
            Instance = httpContext.Request.Path,
            TraceId = httpContext.TraceIdentifier,
            Code = status == HttpStatusCode.BadRequest ? "validation.failed" : "request.failed"
        };
    }
}
