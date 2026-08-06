namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// The result of a sub-agent delegation. Returned to the parent agent
/// so it can incorporate the sub-agent's output into its own reasoning.
/// </summary>
public class SubAgentResult
{
    /// <summary>
    /// The name of the sub-agent that executed.
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// The text output produced by the sub-agent.
    /// May be empty if the sub-agent failed or timed out.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Whether the sub-agent completed successfully without errors.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Optional error message if the sub-agent failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Execution duration in milliseconds for observability.
    /// </summary>
    public double DurationMs { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SubAgentResult Successful(string agentName, string output, double durationMs)
    {
        return new SubAgentResult
        {
            AgentName = agentName,
            Output = output,
            Success = true,
            DurationMs = durationMs
        };
    }

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static SubAgentResult Failed(string agentName, string errorMessage, double durationMs)
    {
        return new SubAgentResult
        {
            AgentName = agentName,
            Output = string.Empty,
            Success = false,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };
    }
}
