namespace AgenticInterview.AgenticSystem.Common;

/// <summary>
/// Standardized result object returned by all agents after execution.
/// Provides a consistent contract for the orchestrator to evaluate agent outcomes.
/// </summary>
public class AgentResult
{
    /// <summary>
    /// Whether the agent completed its task successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The agent that produced this result.
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// The textual output produced by the agent (e.g., a question, an evaluation summary).
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// An optional error message if the agent encountered a failure.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Metadata key-value pairs for extensibility (e.g., "Score", "DifficultyLevel").
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Timestamp when the result was produced.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static AgentResult Success(string agentName, string output, Dictionary<string, object>? metadata = null)
    {
        return new AgentResult
        {
            IsSuccess = true,
            AgentName = agentName,
            Output = output,
            Metadata = metadata ?? new()
        };
    }

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static AgentResult Failure(string agentName, string errorMessage)
    {
        return new AgentResult
        {
            IsSuccess = false,
            AgentName = agentName,
            ErrorMessage = errorMessage
        };
    }
}
