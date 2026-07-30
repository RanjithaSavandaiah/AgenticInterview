using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace AgenticInterview.Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Catches all unhandled exceptions and returns a structured JSON error response
/// instead of leaking stack traces to the client. Implements the Problem Details RFC 7807 pattern.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error: {Message}", ex.Message);
            await WriteErrorResponseAsync(context, HttpStatusCode.BadRequest, "Validation Error", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt.");
            await WriteErrorResponseAsync(context, HttpStatusCode.Unauthorized, "Unauthorized", "You are not authorized to perform this action.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            await WriteErrorResponseAsync(context, HttpStatusCode.NotFound, "Not Found", ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request was cancelled by the client.");
            // Don't write a response; the client has disconnected.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
            await WriteErrorResponseAsync(context, HttpStatusCode.InternalServerError, "Internal Server Error",
                "An unexpected error occurred. Please try again later.");
        }
    }

    /// <summary>
    /// Writes a structured Problem Details (RFC 7807) JSON response.
    /// </summary>
    private static async Task WriteErrorResponseAsync(HttpContext context, HttpStatusCode statusCode, string title, string detail)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            TraceId = Activity.Current?.Id ?? context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}

/// <summary>
/// Extension method to register the global exception middleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
