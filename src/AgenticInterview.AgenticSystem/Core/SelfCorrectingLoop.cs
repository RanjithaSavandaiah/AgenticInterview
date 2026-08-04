using AgenticInterview.AgenticSystem.Common;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// A reusable self-correcting loop executor that implements the core agentic pattern:
///   Execute → Validate → Diagnose → Correct → Retry
/// 
/// Used by agents to wrap LLM calls with output quality validation and automatic
/// re-prompting when the output fails validation (e.g., guardrail rejection,
/// missing question mark, degenerate output).
/// 
/// Also used by the orchestrator to wrap agent execution with retry logic.
/// </summary>
public static class SelfCorrectingLoop
{
    /// <summary>
    /// Executes an action in a self-correcting loop. On each iteration:
    /// 1. Runs the action (which receives a <see cref="SelfCorrectionContext"/> with prior error history)
    /// 2. Validates the result using the provided validator
    /// 3. If valid, returns the result immediately
    /// 4. If invalid, generates corrective feedback and retries
    /// 5. If all retries are exhausted, returns the last result (best-effort)
    /// </summary>
    /// <typeparam name="T">The type of the result produced by the action.</typeparam>
    /// <param name="action">
    /// The async action to execute. Receives a <see cref="SelfCorrectionContext"/> so it can
    /// incorporate corrective feedback from prior failed attempts into its prompt.
    /// </param>
    /// <param name="validator">
    /// Validates the action's output. Returns <see cref="SelfCorrectionValidationResult"/>
    /// indicating whether the output is acceptable.
    /// </param>
    /// <param name="feedbackGenerator">
    /// Given the invalid output and validation failure, generates corrective feedback
    /// that will be passed to the action on the next attempt via <see cref="SelfCorrectionContext.CorrectiveFeedback"/>.
    /// </param>
    /// <param name="options">Configuration for max attempts, delays, and observability metadata.</param>
    /// <param name="logger">Logger for structured observability of the correction loop.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first valid result, or the last result if all retries are exhausted.</returns>
    public static async Task<T> ExecuteAsync<T>(
        Func<SelfCorrectionContext, Task<T>> action,
        Func<T, SelfCorrectionContext, SelfCorrectionValidationResult> validator,
        Func<T, SelfCorrectionValidationResult, SelfCorrectionContext, string> feedbackGenerator,
        SelfCorrectionOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var previousErrors = new List<string>();
        string? cumulativeFeedback = null;
        T lastResult = default!;

        for (int attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = new SelfCorrectionContext
            {
                AttemptNumber = attempt,
                MaxAttempts = options.MaxAttempts,
                PreviousErrors = previousErrors.AsReadOnly(),
                CorrectiveFeedback = cumulativeFeedback,
                AgentName = options.AgentName,
                SessionId = options.SessionId
            };

            // Track the attempt in metrics
            AgentMetrics.SelfCorrectionAttempts.Add(1,
                new KeyValuePair<string, object?>("agent.name", options.AgentName),
                new KeyValuePair<string, object?>("attempt", attempt));

            try
            {
                lastResult = await action(context);
            }
            catch (OperationCanceledException)
            {
                throw; // Don't retry cancellation
            }
            catch (Exception ex)
            {
                // Action itself threw — treat as a validation failure
                logger.LogWarning(ex,
                    "Self-correcting loop action threw on attempt {Attempt}/{MaxAttempts} for agent {AgentName} in session {SessionId}.",
                    attempt, options.MaxAttempts, options.AgentName, options.SessionId);

                var errorReason = $"Action threw exception: {ex.Message}";
                previousErrors.Add(errorReason);
                cumulativeFeedback = BuildCumulativeFeedback(previousErrors);

                if (attempt < options.MaxAttempts && options.RetryDelayMs > 0)
                {
                    await Task.Delay(options.RetryDelayMs * attempt, cancellationToken);
                }

                continue;
            }

            // Validate the result
            var validationResult = validator(lastResult, context);

            if (validationResult.IsValid)
            {
                if (attempt > 1)
                {
                    // Self-correction succeeded!
                    AgentMetrics.SelfCorrectionSuccesses.Add(1,
                        new KeyValuePair<string, object?>("agent.name", options.AgentName),
                        new KeyValuePair<string, object?>("attempt", attempt));

                    logger.LogInformation(
                        "Self-correction succeeded on attempt {Attempt}/{MaxAttempts} for agent {AgentName} in session {SessionId}.",
                        attempt, options.MaxAttempts, options.AgentName, options.SessionId);
                }

                return lastResult;
            }

            // Validation failed — diagnose and prepare corrective feedback
            var failureReason = validationResult.FailureReason ?? "Unknown validation failure";
            previousErrors.Add(failureReason);

            logger.LogWarning(
                "Self-correction validation failed on attempt {Attempt}/{MaxAttempts} for agent {AgentName}: {Reason}",
                attempt, options.MaxAttempts, options.AgentName, failureReason);

            if (attempt < options.MaxAttempts)
            {
                // Generate corrective feedback for the next attempt
                cumulativeFeedback = feedbackGenerator(lastResult, validationResult, context);

                if (options.RetryDelayMs > 0)
                {
                    await Task.Delay(options.RetryDelayMs, cancellationToken);
                }
            }
        }

        // All retries exhausted — return the last result as best-effort
        AgentMetrics.SelfCorrectionExhausted.Add(1,
            new KeyValuePair<string, object?>("agent.name", options.AgentName));

        logger.LogWarning(
            "Self-correcting loop exhausted all {MaxAttempts} attempts for agent {AgentName} in session {SessionId}. " +
            "Returning last result as best-effort. Errors: [{Errors}]",
            options.MaxAttempts, options.AgentName, options.SessionId,
            string.Join("; ", previousErrors));

        return lastResult;
    }

    /// <summary>
    /// Simplified overload for fire-and-forget actions that don't return a value.
    /// Wraps the action in a <see cref="Task{Boolean}"/> returning loop.
    /// </summary>
    public static async Task ExecuteAsync(
        Func<SelfCorrectionContext, Task> action,
        Func<SelfCorrectionContext, SelfCorrectionValidationResult> validator,
        Func<SelfCorrectionValidationResult, SelfCorrectionContext, string> feedbackGenerator,
        SelfCorrectionOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<bool>(
            async ctx =>
            {
                await action(ctx);
                return true;
            },
            (_, ctx) => validator(ctx),
            (_, result, ctx) => feedbackGenerator(result, ctx),
            options,
            logger,
            cancellationToken);
    }

    private static string BuildCumulativeFeedback(List<string> errors)
    {
        return $"Previous attempts failed with the following issues:\n" +
               string.Join("\n", errors.Select((e, i) => $"  Attempt {i + 1}: {e}")) +
               "\nPlease correct these issues in your next response.";
    }
}
