using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.Infrastructure.Services;

/// <summary>
/// A decorating <see cref="IChatClient"/> that caches AI responses for identical prompts.
/// This protects free-tier API quotas (Gemini/Groq) by avoiding duplicate calls
/// when the same prompt is retried (e.g., agent retry loops, network timeouts).
/// 
/// Uses the Decorator Pattern — wraps an inner <see cref="IChatClient"/> transparently.
/// Cache entries have a short TTL (5 minutes) since interview context changes rapidly.
/// </summary>
public class CachedChatClientDecorator : IChatClient
{
    private readonly IChatClient _innerClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedChatClientDecorator> _logger;

    /// <summary>
    /// Short TTL — interview context is dynamic, so we only cache to deduplicate
    /// rapid retries, not to serve stale responses.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public CachedChatClientDecorator(
        IChatClient innerClient,
        IMemoryCache cache,
        ILogger<CachedChatClientDecorator> logger)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = ComputeCacheKey(chatMessages);

        if (_cache.TryGetValue(cacheKey, out ChatResponse? cachedResponse) && cachedResponse != null)
        {
            _logger.LogDebug("AI response cache HIT. Returning cached response (saved an API call).");
            return cachedResponse;
        }

        _logger.LogDebug("AI response cache MISS. Calling LLM...");
        var response = await _innerClient.GetResponseAsync(chatMessages, options, cancellationToken);

        // Only cache successful, non-empty responses
        if (!string.IsNullOrWhiteSpace(response.Text))
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheTtl)
                .SetSize(1);

            _cache.Set(cacheKey, response, cacheOptions);
            _logger.LogDebug("Cached AI response under key hash.");
        }

        return response;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming responses are not cached — they are consumed incrementally
        // and caching them would require buffering the entire stream, negating the benefit.
        return _innerClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _innerClient.Dispose();
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return _innerClient.GetService(serviceType, serviceKey);
    }

    /// <summary>
    /// Computes a deterministic cache key from the chat message content.
    /// Uses SHA256 to produce a fixed-length key regardless of prompt length.
    /// </summary>
    private static string ComputeCacheKey(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            sb.Append(msg.Role.Value);
            sb.Append(':');
            sb.Append(msg.Text ?? string.Empty);
            sb.Append('|');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return $"AiCache_{Convert.ToHexString(hash)}";
    }
}
