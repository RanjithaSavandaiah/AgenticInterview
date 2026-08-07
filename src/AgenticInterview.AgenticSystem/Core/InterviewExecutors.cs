using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Agents;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// A phase executor that runs a group of agents for a specific interview phase.
/// Each phase maps to an <see cref="GoalDefinitions.InterviewGoal"/> and is wired
/// into the <see cref="InterviewOrchestrator"/>'s workflow graph.
///
/// The executor:
/// 1. Selects the agents required for this phase
/// 2. Executes each agent with retry and observability
/// 3. Returns the blackboard (which carries state to the next phase)
/// </summary>
public class PhaseExecutor
{
    private readonly string _phaseName;
    private readonly IReadOnlyList<string> _requiredAgentNames;
    private readonly IReadOnlyList<string> _alwaysActiveAgentNames;
    private readonly ILogger _logger;

    public PhaseExecutor(
        string phaseName,
        IReadOnlyList<string> requiredAgentNames,
        IReadOnlyList<string>? alwaysActiveAgentNames = null,
        ILogger? logger = null)
    {
        _phaseName = phaseName;
        _requiredAgentNames = requiredAgentNames;
        _alwaysActiveAgentNames = alwaysActiveAgentNames ?? [AgenticConstants.ProctoringAgentName];
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    /// <summary>
    /// Executes all agents assigned to this phase against the given blackboard.
    /// Agents are executed sequentially with retry and observability instrumentation.
    /// </summary>
    public async Task ExecuteAsync(
        IList<IAgent> allAgents,
        InterviewBlackboard blackboard,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Phase '{PhaseName}' executing with agents: [{Agents}]",
            _phaseName, string.Join(", ", _requiredAgentNames));

        // Select the agents for this phase: required + always-active
        var activeNames = new HashSet<string>(
            _requiredAgentNames.Concat(_alwaysActiveAgentNames),
            StringComparer.OrdinalIgnoreCase);

        var activeAgents = allAgents.Where(a => activeNames.Contains(a.Name)).ToList();

        foreach (var agent in activeAgents)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            using var activity = AgentMetrics.ActivitySource.StartActivity(
                $"Phase.{_phaseName}.Agent.{agent.Name}",
                ActivityKind.Internal,
                parentContext: default,
                tags:
                [
                    new KeyValuePair<string, object?>("agent.name", agent.Name),
                    new KeyValuePair<string, object?>("phase.name", _phaseName),
                    new KeyValuePair<string, object?>("session.id", blackboard.SessionId.ToString())
                ]);

            var stopwatch = Stopwatch.StartNew();

            for (int retryAttempt = 1; retryAttempt <= AgenticConstants.MaxAgentRetries; retryAttempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    _logger.LogInformation("Executing agent: {AgentName} (attempt {Attempt}/{MaxAttempts})",
                        agent.Name, retryAttempt, AgenticConstants.MaxAgentRetries);
                    AgentMetrics.AgentInvocations.Add(1,
                        new KeyValuePair<string, object?>("agent.name", agent.Name));

                    await agent.ExecuteAsync(blackboard, cancellationToken);

                    stopwatch.Stop();
                    AgentMetrics.AgentExecutionDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("agent.name", agent.Name),
                        new KeyValuePair<string, object?>("status", "success"));

                    _logger.LogInformation("Agent {AgentName} completed in {ElapsedMs}ms.",
                        agent.Name, stopwatch.Elapsed.TotalMilliseconds);
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    AgentMetrics.AgentExecutionDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("agent.name", agent.Name),
                        new KeyValuePair<string, object?>("status", "error"));

                    if (retryAttempt < AgenticConstants.MaxAgentRetries)
                    {
                        var backoffMs = (int)Math.Pow(2, retryAttempt - 1) * 1000;
                        _logger.LogWarning(ex,
                            "Agent {AgentName} failed on attempt {Attempt}/{MaxAttempts}. Retrying in {BackoffMs}ms.",
                            agent.Name, retryAttempt, AgenticConstants.MaxAgentRetries, backoffMs);
                        AgentMetrics.SelfCorrectionAttempts.Add(1,
                            new KeyValuePair<string, object?>("agent.name", agent.Name),
                            new KeyValuePair<string, object?>("attempt", retryAttempt));
                        await Task.Delay(backoffMs, cancellationToken);
                        stopwatch.Restart();
                    }
                    else
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        _logger.LogError(ex,
                            "Agent {AgentName} exhausted all {MaxAttempts} retry attempts.",
                            agent.Name, AgenticConstants.MaxAgentRetries);
                        AgentMetrics.SelfCorrectionExhausted.Add(1,
                            new KeyValuePair<string, object?>("agent.name", agent.Name));
                    }
                }
            }
        }

        _logger.LogInformation("Phase '{PhaseName}' completed.", _phaseName);
    }
}
