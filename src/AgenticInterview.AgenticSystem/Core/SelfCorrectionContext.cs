namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// Carries per-attempt metadata through each iteration of a self-correcting loop.
/// The loop executor populates this before every attempt so the action delegate
/// can incorporate corrective feedback from prior failed attempts.
/// </summary>
public class SelfCorrectionContext
{
    /// <summary>
    /// The current attempt number (1-based). First attempt is 1.
    /// </summary>
    public int AttemptNumber { get; init; } = 1;

    /// <summary>
    /// The maximum number of attempts configured for this loop.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Validation failure reasons from all previous attempts, ordered chronologically.
    /// Empty on the first attempt.
    /// </summary>
    public IReadOnlyList<string> PreviousErrors { get; init; } = [];

    /// <summary>
    /// Cumulative corrective feedback generated from prior validation failures.
    /// Agents should append this to their LLM prompts on retry attempts.
    /// Null on the first attempt.
    /// </summary>
    public string? CorrectiveFeedback { get; init; }

    /// <summary>
    /// The name of the agent executing this loop (for observability).
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// The session ID associated with this loop execution (for observability).
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Whether this is the first attempt (no prior corrections).
    /// </summary>
    public bool IsFirstAttempt => AttemptNumber == 1;

    /// <summary>
    /// Whether this is the final allowed attempt.
    /// </summary>
    public bool IsFinalAttempt => AttemptNumber >= MaxAttempts;
}

/// <summary>
/// The result of validating an agent's output within a self-correcting loop.
/// </summary>
public class SelfCorrectionValidationResult
{
    /// <summary>
    /// Whether the output passed all validation checks.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// A human-readable reason explaining why validation failed.
    /// Used to generate corrective feedback for the next attempt.
    /// </summary>
    public string? FailureReason { get; init; }

    public static SelfCorrectionValidationResult Valid() => new() { IsValid = true };

    public static SelfCorrectionValidationResult Invalid(string reason) => new()
    {
        IsValid = false,
        FailureReason = reason
    };
}

/// <summary>
/// Configuration options for a self-correcting loop instance.
/// </summary>
public class SelfCorrectionOptions
{
    /// <summary>
    /// Maximum number of correction attempts (including the initial attempt).
    /// Default: 3 (1 initial + 2 retries).
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Optional delay between retry attempts in milliseconds.
    /// Set to 0 for no delay. Default: 500ms.
    /// </summary>
    public int RetryDelayMs { get; init; } = 500;

    /// <summary>
    /// The name of the agent executing this loop (for metrics and logging).
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// The session ID (for metrics and logging).
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
