using System.Diagnostics;
using AgenticInterview.AgenticSystem.Agents;
using AgenticInterview.AgenticSystem.AgentCards;
using AgenticInterview.AgenticSystem.Blackboard;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.GoalDefinitions;
using AgenticInterview.AgenticSystem.State;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// The central orchestrator for the multi-agent interview system.
/// Coordinates agent turn-taking, manages the blackboard lifecycle, and
/// implements a goal-driven ReAct (Reason-Act) loop for autonomous agent execution.
/// 
/// The orchestrator:
/// 1. Tracks the current interview phase via <see cref="InterviewGoal"/>
/// 2. Reasons about which agent(s) should act next based on blackboard state
/// 3. Executes the selected agent(s) with full observability instrumentation
/// 4. Advances goals when completion conditions are met
/// </summary>
public class InterviewOrchestrator
{
    private readonly IList<IAgent> _agents;
    private readonly AgentCardRegistry _agentCardRegistry;
    private readonly IReadOnlyList<InterviewGoal> _goals;
    private readonly IChatClient _chatClient;
    private readonly ILogger<InterviewOrchestrator> _logger;

    /// <summary>
    /// Initializes the orchestrator with agents, registry, goals, and an LLM for reasoning.
    /// </summary>
    public InterviewOrchestrator(
        IEnumerable<IAgent> agents,
        AgentCardRegistry agentCardRegistry,
        IChatClient chatClient,
        ILogger<InterviewOrchestrator> logger)
    {
        _agents = agents.ToList();
        _agentCardRegistry = agentCardRegistry;
        _goals = DefaultInterviewGoals.GetAll();
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <summary>
    /// Runs a single orchestration cycle with goal-driven agent selection.
    /// Instead of round-robin, the orchestrator reasons about which agents
    /// should execute based on the current interview phase and blackboard state.
    /// </summary>
    public async Task RunCycleAsync(InterviewBlackboard blackboard, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Orchestrator starting cycle for session {SessionId}", blackboard.SessionId);

        // Determine the current goal
        var currentGoal = GetCurrentGoal(blackboard);
        if (currentGoal != null)
        {
            blackboard.Set(AgenticConstants.CurrentGoalIdKey, currentGoal.Id);
            _logger.LogInformation("Current interview phase: {GoalName} ({GoalId})", currentGoal.Name, currentGoal.Id);
        }

        // Get the agents that should execute in this cycle
        var activeAgents = SelectActiveAgents(blackboard, currentGoal);

        foreach (var agent in activeAgents)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Observability: trace and measure each agent execution
            using var activity = AgentMetrics.ActivitySource.StartActivity(
                $"Agent.{agent.Name}",
                ActivityKind.Internal,
                parentContext: default,
                tags: [
                    new KeyValuePair<string, object?>("agent.name", agent.Name),
                    new KeyValuePair<string, object?>("session.id", blackboard.SessionId.ToString())
                ]);

            var stopwatch = Stopwatch.StartNew();

            // Self-correcting retry loop: retry failed agents with exponential backoff
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
                    break; // Success — exit retry loop
                }
                catch (OperationCanceledException)
                {
                    throw; // Don't retry cancellation
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    AgentMetrics.AgentExecutionDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("agent.name", agent.Name),
                        new KeyValuePair<string, object?>("status", "error"));

                    if (retryAttempt < AgenticConstants.MaxAgentRetries)
                    {
                        var backoffMs = (int)Math.Pow(2, retryAttempt - 1) * 1000; // 1s, 2s
                        _logger.LogWarning(ex,
                            "Agent {AgentName} failed on attempt {Attempt}/{MaxAttempts}. Retrying in {BackoffMs}ms.",
                            agent.Name, retryAttempt, AgenticConstants.MaxAgentRetries, backoffMs);
                        AgentMetrics.SelfCorrectionAttempts.Add(1,
                            new KeyValuePair<string, object?>("agent.name", agent.Name),
                            new KeyValuePair<string, object?>("attempt", retryAttempt));
                        await Task.Delay(backoffMs, cancellationToken);
                        stopwatch.Restart(); // Reset timer for retry
                    }
                    else
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        _logger.LogError(ex,
                            "Agent {AgentName} exhausted all {MaxAttempts} retry attempts. Moving to next agent.",
                            agent.Name, AgenticConstants.MaxAgentRetries);
                        AgentMetrics.SelfCorrectionExhausted.Add(1,
                            new KeyValuePair<string, object?>("agent.name", agent.Name));
                        // Do not post the error to the blackboard, as it breaks the interview immersion
                        // and deadlocks the conversation by changing the lastMessage.SourceAgent.
                    }
                }
            }
        }

        // Check if current goal is completed and advance
        AdvanceGoalIfCompleted(blackboard, currentGoal);

        _logger.LogInformation("Orchestrator cycle complete for session {SessionId}", blackboard.SessionId);
    }

    /// <summary>
    /// Runs the full autonomous interview loop until a termination condition is met
    /// (e.g., session time expired, candidate finished, HR intervention).
    /// </summary>
    public async Task RunFullInterviewAsync(InterviewBlackboard blackboard, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting full autonomous interview for session {SessionId}", blackboard.SessionId);
        AgentMetrics.InterviewsStarted.Add(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            var status = blackboard.Get<string>(AgenticConstants.SessionStatusKey);
            if (status == AgenticConstants.StatusCompleted || status == AgenticConstants.StatusTerminated)
            {
                _logger.LogInformation("Interview session {SessionId} ended with status: {Status}", blackboard.SessionId, status);
                AgentMetrics.InterviewsCompleted.Add(1,
                    new KeyValuePair<string, object?>("status", status));
                break;
            }

            await RunCycleAsync(blackboard, cancellationToken);
            // Brief delay between cycles to prevent CPU spinning
            await Task.Delay(1000, cancellationToken);
        }

        _logger.LogInformation("Full interview loop finished.");
    }

    /// <summary>
    /// Returns the agent cards for all registered agents.
    /// </summary>
    public IReadOnlyCollection<AgentCard> GetRegisteredAgentCards()
    {
        return _agentCardRegistry.GetAll();
    }

    /// <summary>
    /// Determines the current interview goal/phase based on blackboard state.
    /// Goals are checked in order; the first uncompleted goal is the current one.
    /// </summary>
    private InterviewGoal? GetCurrentGoal(InterviewBlackboard blackboard)
    {
        foreach (var goal in _goals)
        {
            var isCompleted = blackboard.Get<string>(goal.CompletionKey);
            if (isCompleted != "true")
            {
                return goal;
            }
        }
        return null; // All goals completed
    }

    /// <summary>
    /// Selects which agents should be active for this cycle based on the current goal.
    /// Falls back to all agents if no goal is active (defensive behavior).
    /// </summary>
    private IEnumerable<IAgent> SelectActiveAgents(InterviewBlackboard blackboard, InterviewGoal? currentGoal)
    {
        // Proctoring agent always runs regardless of phase (security is phase-independent)
        var alwaysActiveAgents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AgenticConstants.ProctoringAgentName
        };

        if (currentGoal == null)
        {
            _logger.LogInformation("No active goal — running all agents (fallback mode).");
            return _agents;
        }

        // Map agent card IDs to agent names for filtering
        var requiredCards = currentGoal.RequiredAgentIds
            .Select(id => _agentCardRegistry.GetById(id))
            .Where(c => c != null)
            .Select(c => c!.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeAgents = _agents.Where(a =>
            requiredCards.Contains(a.Name) ||
            alwaysActiveAgents.Contains(a.Name))
            .ToList();

        _logger.LogInformation("Goal '{GoalName}' selected {Count} agents: [{Agents}]",
            currentGoal.Name, activeAgents.Count, string.Join(", ", activeAgents.Select(a => a.Name)));

        return activeAgents;
    }

    /// <summary>
    /// Checks if the current goal's completion conditions are met and advances to the next goal.
    /// Uses a simple heuristic: count candidate responses to estimate phase progress.
    /// Also implements goal stall detection: if a goal has been active for too many cycles
    /// without advancing, it is force-advanced to prevent infinite loops.
    /// </summary>
    private void AdvanceGoalIfCompleted(InterviewBlackboard blackboard, InterviewGoal? currentGoal)
    {
        if (currentGoal == null) return;

        // --- Goal Stall Detection ---
        // Track how many cycles this goal has been active
        var cycleCountKey = $"{AgenticConstants.GoalCycleCountKeyPrefix}{currentGoal.Id}";
        var currentCycleCount = blackboard.Get<int>(cycleCountKey);
        currentCycleCount++;
        blackboard.Set(cycleCountKey, currentCycleCount);

        var messagesLog = blackboard.GetMessages();
        var candidateResponseCount = messagesLog.Count(m => m.SourceAgent == AgenticConstants.CandidateSourceName);

        // Heuristic: advance goal based on estimated question count per phase
        var shouldAdvance = currentGoal.Id switch
        {
            "goal-intro" => candidateResponseCount >= 1,          // After first candidate response
            "goal-technical" => candidateResponseCount >= 6,      // After ~5 technical Q&A rounds
            "goal-behavioral" => candidateResponseCount >= 9,     // After ~3 more behavioral rounds
            "goal-evaluation" => candidateResponseCount >= 10,    // After evaluation runs
            "goal-closing" => false,                              // Closing ends via session status
            _ => false
        };

        // Self-correcting stall detection: force-advance if stuck for too many cycles
        if (!shouldAdvance && currentCycleCount >= AgenticConstants.MaxGoalStallCycles)
        {
            _logger.LogWarning(
                "Goal '{GoalName}' has been active for {CycleCount} cycles (max: {MaxCycles}). " +
                "Force-advancing to prevent infinite loop. This may indicate agents are not producing " +
                "expected outputs or the candidate is unresponsive.",
                currentGoal.Name, currentCycleCount, AgenticConstants.MaxGoalStallCycles);
            shouldAdvance = true;
        }

        if (shouldAdvance)
        {
            blackboard.Set(currentGoal.CompletionKey, "true");
            blackboard.Set(cycleCountKey, 0); // Reset cycle counter for this goal
            _logger.LogInformation("Goal '{GoalName}' marked as completed. Advancing to next phase.", currentGoal.Name);
        }
    }
}
