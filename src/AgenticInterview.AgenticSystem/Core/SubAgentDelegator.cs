using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.AgentCards;
using AgenticInterview.AgenticSystem.Agents;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// The core delegation engine for sub-agent execution.
/// Allows a parent agent to delegate a sub-task to a child agent, receive the result,
/// and incorporate it into its own reasoning — all within a single orchestration cycle.
///
/// Enforces:
/// - <b>Depth guard</b>: prevents sub-agents from spawning their own sub-agents beyond <see cref="AgenticConstants.MaxSubAgentDepth"/>
/// - <b>Card guard</b>: only allows delegation to agents listed in the parent's <see cref="AgentCard.CanDelegateTo"/>
/// - <b>Timeout guard</b>: cancels sub-agent execution after <see cref="AgenticConstants.SubAgentTimeoutSeconds"/>
/// </summary>
public class SubAgentDelegator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentCardRegistry _agentCardRegistry;
    private readonly ILogger<SubAgentDelegator> _logger;

    public SubAgentDelegator(
        IServiceProvider serviceProvider,
        AgentCardRegistry agentCardRegistry,
        ILogger<SubAgentDelegator> logger)
    {
        _serviceProvider = serviceProvider;
        _agentCardRegistry = agentCardRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Delegates a task to a sub-agent and returns the result.
    /// The sub-agent executes within the parent's orchestration cycle using a snapshot
    /// of the blackboard, and its output is returned to the parent for incorporation.
    /// </summary>
    /// <param name="parentAgentName">The name of the parent agent initiating the delegation.</param>
    /// <param name="targetAgentName">The name of the sub-agent to delegate to.</param>
    /// <param name="taskPrompt">A description of the task for the sub-agent to perform.</param>
    /// <param name="blackboard">The interview blackboard (shared state).</param>
    /// <param name="parentContext">
    /// Optional parent context for enforcing depth limits.
    /// Null if the parent is a top-level agent (depth 0).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SubAgentResult"/> with the sub-agent's output and metadata.</returns>
    public async Task<SubAgentResult> DelegateAsync(
        string parentAgentName,
        string targetAgentName,
        string taskPrompt,
        InterviewBlackboard blackboard,
        SubAgentContext? parentContext = null,
        CancellationToken cancellationToken = default)
    {
        var currentDepth = parentContext?.CurrentDepth ?? 0;
        var maxDepth = parentContext?.MaxDepth ?? AgenticConstants.MaxSubAgentDepth;

        // --- Depth Guard ---
        if (currentDepth >= maxDepth)
        {
            _logger.LogWarning(
                "Sub-agent delegation rejected: depth {CurrentDepth} >= max {MaxDepth}. " +
                "Parent '{ParentAgent}' attempted to delegate to '{TargetAgent}'.",
                currentDepth, maxDepth, parentAgentName, targetAgentName);

            return SubAgentResult.Failed(
                targetAgentName,
                $"Delegation rejected: maximum nesting depth ({maxDepth}) reached. Sub-agents cannot spawn their own sub-agents.",
                0);
        }

        // --- Card Guard ---
        var parentCard = _agentCardRegistry.GetAll()
            .FirstOrDefault(c => c.Name == parentAgentName);

        if (parentCard != null && parentCard.CanDelegateTo.Count > 0)
        {
            // Find the target agent's card ID by name
            var targetCard = _agentCardRegistry.GetAll()
                .FirstOrDefault(c => c.Name == targetAgentName);

            var targetCardId = targetCard?.Id ?? targetAgentName;

            if (!parentCard.CanDelegateTo.Contains(targetCardId, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Sub-agent delegation rejected by card guard: '{ParentAgent}' is not authorized to delegate to '{TargetAgent}' (card ID: '{TargetCardId}'). " +
                    "Allowed targets: [{AllowedTargets}].",
                    parentAgentName, targetAgentName, targetCardId,
                    string.Join(", ", parentCard.CanDelegateTo));

                return SubAgentResult.Failed(
                    targetAgentName,
                    $"Delegation rejected: '{parentAgentName}' is not authorized to delegate to '{targetAgentName}'.",
                    0);
            }
        }

        // --- Resolve the target agent ---
        var agents = _serviceProvider.GetServices<IAgent>();
        var targetAgent = agents.FirstOrDefault(a =>
            string.Equals(a.Name, targetAgentName, StringComparison.OrdinalIgnoreCase));

        if (targetAgent == null)
        {
            _logger.LogError(
                "Sub-agent delegation failed: agent '{TargetAgent}' not found in DI container. " +
                "Available agents: [{AvailableAgents}].",
                targetAgentName,
                string.Join(", ", agents.Select(a => a.Name)));

            return SubAgentResult.Failed(
                targetAgentName,
                $"Agent '{targetAgentName}' not found.",
                0);
        }

        // --- Execute with timeout and tracing ---
        _logger.LogInformation(
            "Delegating sub-task from '{ParentAgent}' to '{TargetAgent}' (depth {Depth}/{MaxDepth}): {Task}",
            parentAgentName, targetAgentName, currentDepth + 1, maxDepth,
            taskPrompt.Length > 200 ? taskPrompt[..200] + "..." : taskPrompt);

        AgentMetrics.SubAgentDelegations.Add(1,
            new KeyValuePair<string, object?>("parent.name", parentAgentName),
            new KeyValuePair<string, object?>("sub.name", targetAgentName));

        using var activity = AgentMetrics.ActivitySource.StartActivity(
            $"Agent.SubAgent.{targetAgentName}",
            ActivityKind.Internal,
            parentContext: default,
            tags:
            [
                new KeyValuePair<string, object?>("parent.agent.name", parentAgentName),
                new KeyValuePair<string, object?>("sub.agent.name", targetAgentName),
                new KeyValuePair<string, object?>("delegation.depth", currentDepth + 1),
                new KeyValuePair<string, object?>("session.id", blackboard.SessionId.ToString())
            ]);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Inject the task prompt into the blackboard so the sub-agent can pick it up
            var delegationTaskKey = $"SubAgent_{targetAgentName}_DelegatedTask";
            blackboard.Set(delegationTaskKey, taskPrompt);

            // Create a timeout-bounded cancellation token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(AgenticConstants.SubAgentTimeoutSeconds));

            await targetAgent.ExecuteAsync(blackboard, timeoutCts.Token);

            stopwatch.Stop();

            // Collect the sub-agent's output from the blackboard
            var messages = blackboard.GetMessages();
            var subAgentOutput = messages
                .Where(m => string.Equals(m.SourceAgent, targetAgentName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault()?.Content ?? string.Empty;

            AgentMetrics.SubAgentDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("parent.name", parentAgentName),
                new KeyValuePair<string, object?>("sub.name", targetAgentName),
                new KeyValuePair<string, object?>("status", "success"));

            _logger.LogInformation(
                "Sub-agent '{TargetAgent}' completed in {ElapsedMs}ms for parent '{ParentAgent}'.",
                targetAgentName, stopwatch.Elapsed.TotalMilliseconds, parentAgentName);

            return SubAgentResult.Successful(targetAgentName, subAgentOutput, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Sub-agent timed out (not parent cancellation)
            stopwatch.Stop();

            AgentMetrics.SubAgentDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("parent.name", parentAgentName),
                new KeyValuePair<string, object?>("sub.name", targetAgentName),
                new KeyValuePair<string, object?>("status", "timeout"));

            _logger.LogWarning(
                "Sub-agent '{TargetAgent}' timed out after {TimeoutSeconds}s (parent: '{ParentAgent}').",
                targetAgentName, AgenticConstants.SubAgentTimeoutSeconds, parentAgentName);

            activity?.SetStatus(ActivityStatusCode.Error, "Timeout");

            return SubAgentResult.Failed(
                targetAgentName,
                $"Sub-agent '{targetAgentName}' timed out after {AgenticConstants.SubAgentTimeoutSeconds}s.",
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw parent cancellation
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            AgentMetrics.SubAgentDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("parent.name", parentAgentName),
                new KeyValuePair<string, object?>("sub.name", targetAgentName),
                new KeyValuePair<string, object?>("status", "error"));

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "Sub-agent '{TargetAgent}' failed with exception (parent: '{ParentAgent}').",
                targetAgentName, parentAgentName);

            return SubAgentResult.Failed(
                targetAgentName,
                $"Sub-agent '{targetAgentName}' failed: {ex.Message}",
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
