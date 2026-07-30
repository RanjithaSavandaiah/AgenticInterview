using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AgenticInterview.Infrastructure.Observability;

/// <summary>
/// Centralized OpenTelemetry-compatible metrics for the interview system.
/// Uses .NET's built-in <see cref="System.Diagnostics.Metrics.Meter"/> API
/// so metrics can be exported to Prometheus, Grafana, Azure Monitor, etc.
/// </summary>
public static class InterviewMetrics
{
    private static readonly Meter Meter = new("AgenticInterview", "1.0.0");

    // --- Counters ---

    /// <summary>
    /// Total number of interview sessions started.
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
        Meter.CreateCounter<long>("proctoring.violations", "events", "Total proctoring violations detected");

    /// <summary>
    /// Total number of AI agent invocations.
    /// </summary>
    public static readonly Counter<long> AgentInvocations =
        Meter.CreateCounter<long>("agent.invocations", "calls", "Total AI agent invocations");

    /// <summary>
    /// Total number of AI fallback activations (primary LLM failed, switched to secondary).
    /// </summary>
    public static readonly Counter<long> AiFallbacks =
        Meter.CreateCounter<long>("ai.fallbacks", "events", "Total AI fallback activations");

    // --- Histograms ---

    /// <summary>
    /// Duration of agent execution in milliseconds.
    /// </summary>
    public static readonly Histogram<double> AgentExecutionDuration =
        Meter.CreateHistogram<double>("agent.execution.duration", "ms", "Agent execution duration in milliseconds");

    /// <summary>
    /// Duration of API request processing in milliseconds.
    /// </summary>
    public static readonly Histogram<double> ApiRequestDuration =
        Meter.CreateHistogram<double>("api.request.duration", "ms", "API request processing duration in milliseconds");

    // --- Activity Sources for Distributed Tracing ---

    /// <summary>
    /// Activity source for distributed tracing across the interview pipeline.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("AgenticInterview");
}
