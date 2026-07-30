using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.Infrastructure.Services;

public class FallbackChatClient : IChatClient
{
    private readonly IChatClient _primaryClient;
    private readonly IChatClient _fallbackClient;
    private readonly ILogger<FallbackChatClient> _logger;

    public FallbackChatClient(IChatClient primaryClient, IChatClient fallbackClient, ILogger<FallbackChatClient> logger)
    {
        _primaryClient = primaryClient ?? throw new ArgumentNullException(nameof(primaryClient));
        _fallbackClient = fallbackClient ?? throw new ArgumentNullException(nameof(fallbackClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Dispose()
    {
        _primaryClient.Dispose();
        _fallbackClient.Dispose();
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Try primary client with retry for 429 rate-limit errors
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await _primaryClient.GetResponseAsync(chatMessages, options, cancellationToken);
            }
            catch (Exception ex) when (Is429RateLimitError(ex))
            {
                if (attempt < 2)
                {
                    var delayMs = (int)Math.Pow(2, attempt + 1) * 1000; // 2s, 4s
                    _logger.LogWarning("Primary ChatClient hit rate limit (429). Retry {Attempt}/3 after {Delay}ms.", attempt + 1, delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }
                else
                {
                    _logger.LogWarning(ex, "Primary ChatClient hit rate limit (429) on final attempt. Falling back to secondary client.");
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary ChatClient failed (non-429). Falling back to secondary client.");
                break; // Non-429 errors fall through to secondary immediately
            }
        }

        // All retries exhausted or non-429 error — use fallback
        _logger.LogWarning("Primary ChatClient exhausted retries. Falling back to secondary client.");
        AgenticInterview.Infrastructure.Observability.InterviewMetrics.AiFallbacks.Add(1);
        return await _fallbackClient.GetResponseAsync(chatMessages, options, cancellationToken);
    }

    private static bool Is429RateLimitError(Exception ex)
    {
        // Check the exception message/inner for 429 status codes
        var message = ex.ToString();
        return message.Contains("429") || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        try
        {
            enumerator = _primaryClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
            if (!await enumerator.MoveNextAsync())
            {
                // Empty stream, but no exception. Just break.
                yield break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Primary ChatClient streaming failed. Falling back to secondary client.");
            // We failed before yielding anything. We can safely fallback.
            if (enumerator != null) await enumerator.DisposeAsync();
            enumerator = null;
        }

        if (enumerator != null)
        {
            // Yield the first item we already moved to
            yield return enumerator.Current;
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
            yield break;
        }

        // If we reach here, we are using the fallback
        await foreach (var update in _fallbackClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return _primaryClient.GetService(serviceType, serviceKey) ?? _fallbackClient.GetService(serviceType, serviceKey);
    }
}
