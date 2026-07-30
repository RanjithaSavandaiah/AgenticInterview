using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace AgenticInterview.Api.ActionFilters;

/// <summary>
/// Action filter that validates the incoming model state and returns
/// a structured 400 Bad Request if validation fails.
/// Applied globally to all controllers to enforce input validation.
/// </summary>
public class ValidationActionFilter : IActionFilter
{
    private readonly ILogger<ValidationActionFilter> _logger;

    public ValidationActionFilter(ILogger<ValidationActionFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            _logger.LogWarning("Model validation failed for {Action}.", context.ActionDescriptor.DisplayName);

            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            context.Result = new BadRequestObjectResult(new
            {
                Status = 400,
                Title = "Validation Failed",
                Errors = errors
            });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No post-action processing needed for validation.
    }
}

/// <summary>
/// Action filter that logs the execution time of each API action.
/// Useful for identifying performance bottlenecks.
/// </summary>
public class PerformanceActionFilter : IActionFilter
{
    private readonly ILogger<PerformanceActionFilter> _logger;
    private Stopwatch? _stopwatch;

    public PerformanceActionFilter(ILogger<PerformanceActionFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        _stopwatch = Stopwatch.StartNew();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        _stopwatch?.Stop();
        var elapsed = _stopwatch?.ElapsedMilliseconds ?? 0;

        if (elapsed > 500)
        {
            _logger.LogWarning("Slow action detected: {Action} took {ElapsedMs}ms",
                context.ActionDescriptor.DisplayName, elapsed);
        }
        else
        {
            _logger.LogDebug("Action {Action} completed in {ElapsedMs}ms",
                context.ActionDescriptor.DisplayName, elapsed);
        }
    }
}
