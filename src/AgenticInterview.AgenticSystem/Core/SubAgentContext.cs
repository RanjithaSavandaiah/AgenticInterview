namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// Holds metadata about the current sub-agent delegation chain.
/// Passed to sub-agents so they can inspect their position in the hierarchy
/// and enforce depth limits to prevent runaway nesting.
/// </summary>
public class SubAgentContext
{
    /// <summary>
    /// The name of the agent that initiated this delegation.
    /// </summary>
    public string ParentAgentName { get; init; } = string.Empty;

    /// <summary>
    /// The current nesting depth. 0 = top-level agent, 1 = sub-agent.
    /// </summary>
    public int CurrentDepth { get; init; }

    /// <summary>
    /// The maximum allowed nesting depth. Delegation is rejected when
    /// <see cref="CurrentDepth"/> >= <see cref="MaxDepth"/>.
    /// </summary>
    public int MaxDepth { get; init; } = Common.AgenticConstants.MaxSubAgentDepth;

    /// <summary>
    /// A description of the task the parent agent wants the sub-agent to perform.
    /// Injected into the sub-agent's prompt context.
    /// </summary>
    public string TaskDescription { get; init; } = string.Empty;

    /// <summary>
    /// Returns true if the current depth has reached the maximum, preventing further delegation.
    /// </summary>
    public bool IsAtMaxDepth => CurrentDepth >= MaxDepth;
}
