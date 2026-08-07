using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// implements a phase-based workflow for autonomous agent execution.
///
/// The orchestrator models the interview as a directed graph of phases:
/// <code>
///   Intro → Technical → Behavioral → Evaluation → Closing
///           ↑ Proctoring runs concurrently at every phase ↑
/// </code>
///
/// Each phase is backed by a <see cref="PhaseExecutor"/> that groups the agents
/// required for that stage. The orchestrator:
/// 1. Tracks the current interview phase via <see cref="InterviewGoal"/>
/// 2. Delegates agent execution to the appropriate <see cref="PhaseExecutor"/>
/// 3. Advances phases when completion conditions are met
/// 4. Detects stalled phases and force-advances to prevent infinite loops
/// </summary>
public class InterviewOrchestrator
{
    private readonly IList<IAgent> _agents;
    private readonly AgentCardRegistry _agentCardRegistry;
    private readonly IReadOnlyList<InterviewGoal> _goals;
    private readonly IChatClient _chatClient;
    private readonly ILogger<InterviewOrchestrator> _logger;

    /// <summary>
    /// Phase executors keyed by goal ID. Each executor knows which agents to run
    /// for its interview phase. Built once at construction time from the goal definitions.
    /// </summary>
    private readonly Dictionary<string, PhaseExecutor> _phaseExecutors;

    /// <summary>
    /// Initializes the orchestrator with agents, registry, goals, and an LLM for reasoning.
    /// Builds the phase executor graph from the goal definitions.
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

        // Build the phase executor graph: each goal maps to a PhaseExecutor
        // that knows which agents to run for that interview stage.
        _phaseExecutors = BuildPhaseExecutors();
    }

    /// <summary>
    /// Runs a single orchestration cycle with phase-based agent selection.
    /// Instead of round-robin, the orchestrator delegates to the <see cref="PhaseExecutor"/>
    /// for the current interview phase, which runs only the agents required for that stage.
    /// </summary>
    public async Task RunCycleAsync(InterviewBlackboard blackboard, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Orchestrator starting cycle for session {SessionId}", blackboard.SessionId);

        // Determine the current goal/phase
        var currentGoal = GetCurrentGoal(blackboard);
        if (currentGoal != null)
        {
            blackboard.Set(AgenticConstants.CurrentGoalIdKey, currentGoal.Id);
            _logger.LogInformation("Current interview phase: {GoalName} ({GoalId})", currentGoal.Name, currentGoal.Id);
        }

        // Delegate to the phase executor for the current goal
        if (currentGoal != null && _phaseExecutors.TryGetValue(currentGoal.Id, out var executor))
        {
            await executor.ExecuteAsync(_agents, blackboard, cancellationToken);
        }
        else
        {
            // Fallback: no active goal or no executor — run all agents
            _logger.LogInformation("No active phase — running all agents (fallback mode).");
            var fallbackExecutor = new PhaseExecutor(
                "Fallback",
                _agents.Select(a => a.Name).ToList(),
                logger: _logger);
            await fallbackExecutor.ExecuteAsync(_agents, blackboard, cancellationToken);
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
    /// Builds the phase executor graph from the goal definitions.
    /// Each goal's <see cref="InterviewGoal.RequiredAgentIds"/> are resolved to agent names
    /// via the <see cref="AgentCardRegistry"/> and wrapped in a <see cref="PhaseExecutor"/>.
    /// </summary>
    private Dictionary<string, PhaseExecutor> BuildPhaseExecutors()
    {
        var executors = new Dictionary<string, PhaseExecutor>(StringComparer.OrdinalIgnoreCase);

        foreach (var goal in _goals)
        {
            // Resolve agent card IDs to agent names
            var agentNames = goal.RequiredAgentIds
                .Select(id => _agentCardRegistry.GetById(id))
                .Where(c => c != null)
                .Select(c => c!.Name)
                .ToList();

            executors[goal.Id] = new PhaseExecutor(
                phaseName: goal.Name,
                requiredAgentNames: agentNames,
                alwaysActiveAgentNames: [AgenticConstants.ProctoringAgentName],
                logger: _logger);

            _logger.LogDebug("Built phase executor '{PhaseName}' with agents: [{Agents}]",
                goal.Name, string.Join(", ", agentNames));
        }

        return executors;
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
