using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace AgenticInterview.Api.ActionFilters;

/// <summary>
/// Action filter attribute that enforces idempotency on mutating (POST/PUT/PATCH) API endpoints.
/// 
/// The client must send an <c>X-Idempotency-Key</c> header with a unique value (e.g., a GUID).
/// If the same key has been seen before within the TTL window, the server returns the
/// previously cached response instead of re-executing the action.
/// 
/// This prevents:
/// - Double-counted proctoring strikes (unfair candidate termination)
/// - Duplicate interview sessions from network retries
/// - Duplicate code/answer submissions from double-clicks
/// 
/// Uses <see cref="IMemoryCache"/> — no external infrastructure required.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class IdempotentAttribute : Attribute, IFilterFactory
{
    /// <summary>
    /// How long to remember a processed idempotency key.
    /// Default: 10 minutes — enough to cover retries without consuming too much memory.
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 600;

    /// <summary>
    /// Whether the idempotency key header is required. If true, requests without it get a 400.
    /// If false, requests without the header are processed normally (no idempotency enforcement).
    /// </summary>
    public bool IsRequired { get; set; } = true;

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyFilter>>();
        return new IdempotencyFilter(cache, logger, CacheDurationSeconds, IsRequired);
    }
}

/// <summary>
/// The internal filter that does the actual idempotency check.
/// Separated from the attribute to allow constructor injection.
/// </summary>
public class IdempotencyFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<IdempotencyFilter> _logger;
    private readonly int _cacheDurationSeconds;
    private readonly bool _isRequired;

    /// <summary>
    /// The HTTP header name for the idempotency key.
    /// </summary>
    private const string IdempotencyKeyHeader = "X-Idempotency-Key";

    /// <summary>
    /// Cache key prefix to namespace idempotency entries.
    /// </summary>
    private const string CachePrefix = "Idempotency_";

    public IdempotencyFilter(IMemoryCache cache, ILogger<IdempotencyFilter> logger, int cacheDurationSeconds, bool isRequired)
    {
        _cache = cache;
        _logger = logger;
        _cacheDurationSeconds = cacheDurationSeconds;
        _isRequired = isRequired;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. Extract the idempotency key from the request header
        if (!context.HttpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            if (_isRequired)
            {
                _logger.LogWarning("Request to {Action} missing required {Header} header.",
                    context.ActionDescriptor.DisplayName, IdempotencyKeyHeader);

                context.Result = new BadRequestObjectResult(new
                {
                    Status = 400,
                    Title = "Missing Idempotency Key",
                    Detail = $"The '{IdempotencyKeyHeader}' header is required for this endpoint."
                });
                return;
            }

            // Not required — proceed without idempotency check
            await next();
            return;
        }

        var idempotencyKey = keyValues.First()!;
        var cacheKey = $"{CachePrefix}{context.HttpContext.Request.Path}_{idempotencyKey}";

        // 2. Check if this key has already been processed
        if (_cache.TryGetValue(cacheKey, out IdempotentCachedResponse? cachedResponse) && cachedResponse != null)
        {
            _logger.LogInformation("Idempotency key '{Key}' already processed. Returning cached response (HTTP {StatusCode}).",
                idempotencyKey, cachedResponse.StatusCode);

            context.Result = new ObjectResult(cachedResponse.Body)
            {
                StatusCode = cachedResponse.StatusCode
            };
            return;
        }

        // 3. Execute the action
        var executedContext = await next();

        // 4. Cache the response if the action succeeded
        if (executedContext.Result is ObjectResult objectResult)
        {
            var responseToCache = new IdempotentCachedResponse
            {
                StatusCode = objectResult.StatusCode ?? 200,
                Body = objectResult.Value
            };

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(_cacheDurationSeconds))
                .SetSize(1);

            _cache.Set(cacheKey, responseToCache, cacheOptions);

            _logger.LogDebug("Cached response for idempotency key '{Key}' (TTL: {Ttl}s).", idempotencyKey, _cacheDurationSeconds);
        }
    }
}

/// <summary>
/// Internal model for caching the HTTP response associated with an idempotency key.
/// </summary>
internal class IdempotentCachedResponse
{
    public int StatusCode { get; init; }
    public object? Body { get; init; }
}
