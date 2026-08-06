using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AgenticInterview.AgenticSystem.Common;

/// <summary>
/// OpenTelemetry-compatible metrics for the agentic system.
/// Uses the .NET BCL <see cref="System.Diagnostics.Metrics.Meter"/> API
/// so metrics can be exported to Prometheus, Grafana, Azure Monitor, etc.
/// 
/// These are separated from Infrastructure.Observability.InterviewMetrics
/// to keep the AgenticSystem layer independent of the Infrastructure layer.
/// Both meter sources share the same "AgenticInterview" meter name so they
/// are exported together as a single logical meter.
/// </summary>
public static class AgentMetrics
{
    private static readonly Meter Meter = new("AgenticInterview.Agents", "1.0.0");

    /// <summary>
    /// Total number of AI agent invocations across all sessions.
    /// </summary>
    public static readonly Counter<long> AgentInvocations =
        Meter.CreateCounter<long>("agent.invocations", "calls", "Total AI agent invocations");

    /// <summary>
    /// Duration of agent execution in milliseconds.
    /// </summary>
    public static readonly Histogram<double> AgentExecutionDuration =
        Meter.CreateHistogram<double>("agent.execution.duration", "ms", "Agent execution duration in milliseconds");

    /// <summary>
    /// Total number of interview sessions started (tracked at orchestrator level).
    /// </summary>
    public static readonly Counter<long> InterviewsStarted =
        Meter.CreateCounter<long>("interviews.started", "sessions", "Total interviews started");

    /// <summary>
    /// Total number of interview sessions completed.
    /// </summary>
    public static readonly Counter<long> InterviewsCompleted =
        Meter.CreateCounter<long>("interviews.completed", "sessions", "Total interviews completed");

    /// <summary>
    /// Total number of proctoring violations detected.
    /// </summary>
    public static readonly Counter<long> ProctoringViolations =
        Meter.CreateCounter<long>("proctoring.violations", "events", "Total proctoring violations");

    /// <summary>
    /// Total number of AI fallback activations.
    /// </summary>
    public static readonly Counter<long> AiFallbacks =
        Meter.CreateCounter<long>("ai.fallbacks", "events", "Total AI fallback activations");

    // --- Self-Correcting Loop Metrics ---

    /// <summary>
    /// Total number of self-correction loop iterations across all agents.
    /// Tagged with agent.name and attempt number for per-agent analysis.
    /// </summary>
    public static readonly Counter<long> SelfCorrectionAttempts =
        Meter.CreateCounter<long>("agent.self_correction.attempts", "attempts", "Total self-correction loop iterations");

    /// <summary>
    /// Corrections that produced valid output after a prior validation failure.
    /// A high count indicates agents are successfully self-healing.
    /// </summary>
    public static readonly Counter<long> SelfCorrectionSuccesses =
        Meter.CreateCounter<long>("agent.self_correction.successes", "corrections", "Successful self-corrections after prior failure");

    /// <summary>
    /// Self-correcting loops that exhausted all retries without producing valid output.
    /// A high count indicates prompts or validators need tuning.
    /// </summary>
    public static readonly Counter<long> SelfCorrectionExhausted =
        Meter.CreateCounter<long>("agent.self_correction.exhausted", "exhaustions", "Self-correction loops that exhausted all retries");

    // --- Sub-Agent Delegation Metrics ---

    /// <summary>
    /// Total number of sub-agent delegations. Tagged with parent.name and sub.name
    /// for analyzing delegation patterns between agents.
    /// </summary>
    public static readonly Counter<long> SubAgentDelegations =
        Meter.CreateCounter<long>("agent.sub_agent.delegations", "delegations", "Total sub-agent delegations");

    /// <summary>
    /// Duration of sub-agent execution in milliseconds. Tagged with parent.name,
    /// sub.name, and status (success/error) for latency analysis.
    /// </summary>
    public static readonly Histogram<double> SubAgentDuration =
        Meter.CreateHistogram<double>("agent.sub_agent.duration", "ms", "Sub-agent execution duration in milliseconds");

    /// <summary>
    /// Activity source for distributed tracing of agent execution.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("AgenticInterview.Agents");
}
