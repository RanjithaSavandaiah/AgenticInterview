using System.Text.Json.Serialization;

namespace AgenticInterview.AgenticSystem.AgentCards;

/// <summary>
/// Represents an A2A (Agent-to-Agent) Agent Card conforming to Google's A2A protocol.
/// Each agent in the system advertises its capabilities, skills, and endpoint via an AgentCard.
/// This enables dynamic agent discovery and orchestration.
/// </summary>
public class AgentCard
{
    /// <summary>
    /// A unique identifier for the agent (e.g., "technical-interviewer").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "Technical Interviewer Agent").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// A detailed description of the agent's purpose and capabilities.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The agent's operational goal in the multi-agent system.
    /// </summary>
    public string Goal { get; init; } = string.Empty;

    /// <summary>
    /// A list of skill descriptors this agent can perform.
    /// </summary>
    public IReadOnlyList<AgentSkill> Skills { get; init; } = [];

    /// <summary>
    /// The supported input content types (e.g., "text/plain", "application/json").
    /// </summary>
    public IReadOnlyList<string> InputContentTypes { get; init; } = ["text/plain"];

    /// <summary>
    /// The supported output content types.
    /// </summary>
    public IReadOnlyList<string> OutputContentTypes { get; init; } = ["text/plain"];

    /// <summary>
    /// Whether this agent supports streaming responses.
    /// </summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>
    /// The agent card IDs that this agent is allowed to delegate sub-tasks to.
    /// Acts as a guardrail preventing arbitrary agent-to-agent delegation.
    /// An empty list means this agent cannot delegate to any sub-agents.
    /// </summary>
    public IReadOnlyList<string> CanDelegateTo { get; init; } = [];

    /// <summary>
    /// Optional endpoint URL for the agent (used in distributed A2A scenarios).
    /// </summary>
    public string? EndpointUrl { get; init; }
}

/// <summary>
/// Describes a specific skill or capability an agent can perform.
/// </summary>
public class AgentSkill
{
    /// <summary>
    /// Unique skill identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable skill name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Description of what this skill does.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Tags for categorization and discovery.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
