using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// A hosted background service that manages the lifecycle of interview orchestration loops.
/// Replaces raw <c>Task.Run</c> fire-and-forget patterns with a structured, cancellation-aware
/// background service that supports graceful shutdown.
/// 
/// Uses <see cref="Channel{T}"/> for backpressure-aware work queuing and tracks running
/// sessions so they can be individually cancelled (e.g., HR intervention) or collectively
/// drained on application shutdown.
/// </summary>
public class InterviewBackgroundService : BackgroundService
{
    private readonly Channel<InterviewWorkItem> _workQueue;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningSessions = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InterviewBackgroundService> _logger;

    public InterviewBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<InterviewBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Bounded channel prevents unbounded memory growth if sessions are enqueued faster than processed
        _workQueue = Channel.CreateBounded<InterviewWorkItem>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Enqueues a new interview session to be started in the background.
    /// Called by the event handler instead of Task.Run.
    /// </summary>
    public async ValueTask EnqueueSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var workItem = new InterviewWorkItem(sessionId);
        await _workQueue.Writer.WriteAsync(workItem, cancellationToken);
        _logger.LogInformation("Enqueued interview session {SessionId} for background processing.", sessionId);
    }

    /// <summary>
    /// Cancels a specific running interview session (e.g., HR intervention or timeout).
    /// </summary>
    public bool CancelSession(Guid sessionId)
    {
        if (_runningSessions.TryRemove(sessionId, out var cts))
        {
            // Cancel but do NOT dispose here — RunSessionAsync's finally block handles disposal.
            // Disposing here would cause ObjectDisposedException in the running task.
            cts.Cancel();
            _logger.LogInformation("Cancelled running interview session {SessionId}.", sessionId);
            return true;
        }

        _logger.LogWarning("Attempted to cancel session {SessionId} but it was not found in running sessions.", sessionId);
        return false;
    }

    /// <summary>
    /// Returns the IDs of all currently running interview sessions.
    /// </summary>
    public IReadOnlyCollection<Guid> GetRunningSessions()
    {
        return _runningSessions.Keys.ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InterviewBackgroundService started. Waiting for interview sessions...");

        await foreach (var workItem in _workQueue.Reader.ReadAllAsync(stoppingToken))
        {
            // Create a linked token that cancels when either the app stops or the session is individually cancelled
            var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _runningSessions[workItem.SessionId] = sessionCts;

            // Run each session in its own task so we can process multiple sessions concurrently
            _ = RunSessionAsync(workItem.SessionId, sessionCts.Token);
        }
    }

    private async Task RunSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting interview orchestration for session {SessionId}.", sessionId);

        try
        {
            for (int attempt = 1; attempt <= Common.AgenticConstants.MaxSessionRetries + 1; attempt++)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var orchestrator = scope.ServiceProvider.GetRequiredService<InterviewOrchestrator>();
                    var blackboardManager = scope.ServiceProvider.GetRequiredService<IBlackboardManager>();
                    var blackboard = blackboardManager.GetOrCreate(sessionId);

                    // Load contextual data into the blackboard for the agents
                    var sessionRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IRepository<Domain.Entities.InterviewSession>>();
                    var candidateRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IRepository<Domain.Entities.CandidateProfile>>();
                    var jdRepo = scope.ServiceProvider.GetRequiredService<Domain.Interfaces.IRepository<Domain.Entities.JobDescriptionProfile>>();

                    var session = await sessionRepo.GetByIdAsync(sessionId);
                    if (session != null)
                    {
                        var candidate = await candidateRepo.GetByIdAsync(session.CandidateProfileId);
                        var jd = await jdRepo.GetByIdAsync(session.JobDescriptionId);

                        if (candidate != null)
                        {
                            blackboard.Set(Common.AgenticConstants.CandidateResumeTextKey, candidate.ResumeTextContent);
                            blackboard.Set(Common.AgenticConstants.CandidateNameKey, candidate.Name);
                        }
                        if (jd != null) blackboard.Set(Common.AgenticConstants.JobDescriptionKey, jd.DescriptionTextContent);
                    }

                    await orchestrator.RunFullInterviewAsync(blackboard, cancellationToken);

                    _logger.LogInformation("Interview orchestration completed for session {SessionId}.", sessionId);
                    return; // Success — exit retry loop
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Interview session {SessionId} was cancelled.", sessionId);
                    return; // Cancellation is intentional — don't retry
                }
                catch (Exception ex) when (IsTransientError(ex) && attempt <= Common.AgenticConstants.MaxSessionRetries)
                {
                    // Transient error — retry with exponential backoff
                    var backoffMs = Common.AgenticConstants.BaseSessionRetryDelayMs * (int)Math.Pow(2, attempt - 1);
                    _logger.LogWarning(ex,
                        "Transient failure in session {SessionId} on attempt {Attempt}/{MaxAttempts}. " +
                        "Retrying in {BackoffMs}ms.",
                        sessionId, attempt, Common.AgenticConstants.MaxSessionRetries + 1, backoffMs);

                    Common.AgentMetrics.SelfCorrectionAttempts.Add(1,
                        new KeyValuePair<string, object?>("agent.name", "SessionOrchestration"),
                        new KeyValuePair<string, object?>("attempt", attempt));

                    await Task.Delay(backoffMs, cancellationToken);
                    // Loop continues to next attempt with a fresh DI scope
                }
                catch (Exception ex)
                {
                    // Permanent/non-transient error or final attempt — give up
                    _logger.LogError(ex, "Failed to run agentic loop for session {SessionId} (attempt {Attempt}). No more retries.",
                        sessionId, attempt);

                    if (attempt > 1)
                    {
                        Common.AgentMetrics.SelfCorrectionExhausted.Add(1,
                            new KeyValuePair<string, object?>("agent.name", "SessionOrchestration"));
                    }
                    return;
                }
            }

            // Should not reach here, but log defensively
            _logger.LogError("Session {SessionId} exhausted all retry attempts.", sessionId);
        }
        finally
        {
            // Always clean up the running session entry regardless of outcome
            if (_runningSessions.TryRemove(sessionId, out var cts))
            {
                cts.Dispose();
            }
        }
    }

    /// <summary>
    /// Heuristic to determine if an exception represents a transient failure
    /// that is worth retrying (network issues, LLM provider 5xx/429 errors).
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("429", StringComparison.Ordinal) ||
               message.Contains("500", StringComparison.Ordinal) ||
               message.Contains("502", StringComparison.Ordinal) ||
               message.Contains("503", StringComparison.Ordinal) ||
               message.Contains("504", StringComparison.Ordinal) ||
               message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
               ex is System.Net.Http.HttpRequestException ||
               ex is TimeoutException ||
               ex is System.IO.IOException;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InterviewBackgroundService stopping. Draining {Count} running sessions...", _runningSessions.Count);

        // Signal the channel that no more items will be written
        _workQueue.Writer.Complete();

        // Cancel all running sessions gracefully
        foreach (var kvp in _runningSessions)
        {
            kvp.Value.Cancel();
        }

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("InterviewBackgroundService stopped.");
    }
}

/// <summary>
/// Represents a unit of work for the interview background service.
/// </summary>
public record InterviewWorkItem(Guid SessionId);
