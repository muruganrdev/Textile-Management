using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.Common.Exceptions;

namespace SareeStore.Api.Middleware;

/// <summary>
/// Centralized global exception handler mapping application domain exceptions to RFC 7807 Problem Details.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        ProblemDetails problemDetails;
        int statusCode;

        switch (exception)
        {
            case ValidationException validationException:
                statusCode = StatusCodes.Status400BadRequest;
                _logger.LogWarning("Validation failure on {Path}: {Message}. TraceId: {TraceId}",
                    httpContext.Request.Path, validationException.Message, traceId);

                problemDetails = new HttpValidationProblemDetails(validationException.Errors)
                {
                    Status = statusCode,
                    Title = "One or more validation errors occurred.",
                    Detail = validationException.Message,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                };
                break;

            case NotFoundException notFoundException:
                statusCode = StatusCodes.Status404NotFound;
                _logger.LogWarning("Resource not found on {Path}: {Message}. TraceId: {TraceId}",
                    httpContext.Request.Path, notFoundException.Message, traceId);

                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Resource Not Found",
                    Detail = notFoundException.Message,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5"
                };
                break;

            case ConflictException conflictException:
                statusCode = StatusCodes.Status409Conflict;
                _logger.LogWarning("Business conflict on {Path}: {Message}. TraceId: {TraceId}",
                    httpContext.Request.Path, conflictException.Message, traceId);

                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Business Conflict",
                    Detail = conflictException.Message,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
                };
                break;

            case UnauthorizedException unauthorizedException:
                statusCode = StatusCodes.Status401Unauthorized;
                _logger.LogWarning("Unauthorized access on {Path}: {Message}. TraceId: {TraceId}",
                    httpContext.Request.Path, unauthorizedException.Message, traceId);

                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Unauthorized",
                    Detail = unauthorizedException.Message,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
                };
                break;

            case ForbiddenException forbiddenException:
                statusCode = StatusCodes.Status403Forbidden;
                _logger.LogWarning("Forbidden access on {Path}: {Message}. TraceId: {TraceId}",
                    httpContext.Request.Path, forbiddenException.Message, traceId);

                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "Forbidden",
                    Detail = forbiddenException.Message,
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
                };
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                _logger.LogError(exception, "Unhandled exception on {Path}. TraceId: {TraceId}",
                    httpContext.Request.Path, traceId);

                problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = "An unexpected server error occurred.",
                    Detail = "An unexpected server error occurred. Please contact support referencing the trace ID.",
                    Instance = httpContext.Request.Path,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
                };
                break;
        }

        problemDetails.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), JsonOptions);
        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }
}
