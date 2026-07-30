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

    /// <summary>
    /// Activity source for distributed tracing of agent execution.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("AgenticInterview.Agents");
}
