using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.Guardrails;
using AgenticInterview.AgenticSystem.Memory;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

#pragma warning disable MAAI001 // Experimental MAF Agent Harness APIs

namespace AgenticInterview.AgenticSystem.Agents;

/// <summary>
/// Base class for all agents in the multi-agent interview system.
/// Provides shared infrastructure: LLM client, tools, memory store, guardrails, and logging.
/// 
/// Agents extend this class and implement <see cref="ExecuteCoreAsync"/> to define their behavior.
/// The base class handles cross-cutting concerns like memory persistence, output validation,
/// and observability instrumentation.
/// 
/// Each agent wraps its <see cref="IChatClient"/> with the MAF <see cref="AIAgent"/> harness,
/// providing automatic context compaction and instruction merging.
/// 
/// Tools are resolved per-agent via <see cref="AgentToolResolver"/>: each agent receives only
/// the MCP tools matching its agent card skills (e.g., ProctoringAgent gets record_proctoring_event
/// but NOT search_web). If no resolver is available, falls back to the full tool set.
/// </summary>
public abstract class BaseAgent : IAgent
{
    public string Name { get; }
    public string Goal { get; }
    
    protected readonly IChatClient ChatClient;
    protected readonly ILogger Logger;
    protected readonly IList<AITool> Tools;
    protected readonly IConversationMemoryStore MemoryStore;
    protected readonly AgentGuardrails Guardrails;

    /// <summary>
    /// The MAF Agent Harness wrapping the raw <see cref="IChatClient"/>.
    /// Provides automatic context compaction, instruction merging, and multi-turn tool orchestration.
    /// </summary>
    protected readonly ChatClientAgent HarnessAgent;

    /// <summary>
    /// Optional sub-agent delegator for spawning child agents within this agent's execution.
    /// Nullable to maintain backward compatibility with unit tests that construct agents without DI.
    /// </summary>
    protected readonly SubAgentDelegator? Delegator;

    protected BaseAgent(
        string name,
        string goal,
        IChatClient chatClient,
        ILogger logger,
        IList<AITool> allTools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgentToolResolver? toolResolver = null,
        IServiceProvider? serviceProvider = null,
        SubAgentDelegator? delegator = null)
    {
        Name = name;
        Goal = goal;
        ChatClient = chatClient;
        Logger = logger;
        MemoryStore = memoryStore;
        Guardrails = guardrails;
        Delegator = delegator;

        // Resolve per-agent tools via skill mapping if resolver is available.
        // This ensures each agent only gets the MCP tools matching its agent card skills.
        // Falls back to the full tool set if no resolver (e.g., in unit tests).
        if (toolResolver != null && serviceProvider != null)
        {
            var toolLogger = serviceProvider.GetService(typeof(ILoggerFactory)) is ILoggerFactory lf
                ? lf.CreateLogger(typeof(McpTools.InterviewMcpToolFactory))
                : logger;
            Tools = toolResolver.ResolveToolsForAgent(name, toolLogger, serviceProvider);
        }
        else
        {
            Tools = allTools;
        }

        // Initialize the MAF ChatClientAgent harness with per-agent configurable options
        HarnessAgent = new ChatClientAgent(ChatClient, GetAgentOptions());
    }

    /// <summary>
    /// Returns the MAF harness configuration for this agent. Concrete agents can override
    /// this to customize instructions and tool configuration.
    /// 
    /// The default configuration includes:
    /// - <see cref="CompactionProvider"/> with <see cref="ContextWindowCompactionStrategy"/> for
    ///   automatic tool-result eviction and hard truncation within a single strategy
    /// - <see cref="FileAccessProvider"/> for session-scoped file memory (replaces hand-rolled memory store)
    /// </summary>
    protected virtual ChatClientAgentOptions GetAgentOptions()
    {
        var providers = new List<AIContextProvider>
        {
            // Context window management: ContextWindowCompactionStrategy is an all-in-one
            // strategy that applies tool-result eviction at 50% capacity and hard truncation
            // at 80% capacity — replacing the need for a manual composed pipeline.
            new CompactionProvider(GetCompactionStrategy())
        };

        // Wire up FileAccessProvider for session-scoped file memory if a store path is available.
        // This replaces the hand-rolled ConversationMemoryStore with MAF's built-in file access
        // provider, which automatically exposes file_access_read_file / file_access_save_file tools.
        var fileStore = GetFileStore();
        if (fileStore != null)
        {
            providers.Add(new FileAccessProvider(fileStore));
        }

        return new ChatClientAgentOptions
        {
            Name = Name,
            Description = Goal,
            ChatOptions = new ChatOptions
            {
                Tools = Tools
            },
            AIContextProviders = providers
        };
    }

    /// <summary>
    /// Returns the compaction strategy for headroom management. Concrete agents can override
    /// this to customize when and how compaction occurs.
    /// 
    /// Default: <see cref="ContextWindowCompactionStrategy"/> with a 128K token context window
    /// and 4K max output tokens. This all-in-one strategy handles:
    /// - <b>Tool result eviction</b> at 50% capacity (~62K tokens) — collapses verbose tool
    ///   call/result pairs into concise summaries
    /// - <b>Hard truncation</b> at 80% capacity (~99K tokens) — removes oldest non-system
    ///   message groups as a fail-safe
    /// </summary>
    protected virtual CompactionStrategy GetCompactionStrategy()
    {
        return new ContextWindowCompactionStrategy(
            maxContextWindowTokens: 128_000,
            maxOutputTokens: 4_096
        );
    }

    /// <summary>
    /// Returns the file store for session-scoped file memory. Concrete agents can override
    /// this to customize the storage location. Returns null to disable file access.
    /// </summary>
    protected virtual AgentFileStore? GetFileStore()
    {
        return null; // Disabled by default; agents opt-in by overriding
    }

    /// <summary>
    /// Executes the agent with memory retrieval and output guardrails wrapped around the core logic.
    /// </summary>
    public async Task ExecuteAsync(InterviewBlackboard blackboard, CancellationToken cancellationToken = default)
    {
        // Retrieve relevant memories for this agent's context.
        // Memory retrieval is best effort enrichment the agent runs even if memory is unavailable.
        // Note: The MAF FileAccessProvider (if configured) provides additional session scoped
        // file memory automatically. This manual retrieval supplements it with cross agent context.
        var sessionId = blackboard.SessionId.ToString();
        var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        
        IEnumerable<string> relevantMemories = [];
        if (!string.IsNullOrWhiteSpace(currentTranscript))
        {
            try
            {
                relevantMemories = await MemoryStore.RetrieveRelevantMemoriesAsync(sessionId, currentTranscript);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to retrieve memories for agent {AgentName}. Proceeding without context.", Name);
            }
        }

        // Execute the agent's core logic
        await ExecuteCoreAsync(blackboard, relevantMemories, cancellationToken);
    }

    /// <summary>
    /// Core agent logic implemented by each concrete agent.
    /// Receives relevant memories from previous interactions for context enrichment.
    /// </summary>
    protected abstract Task ExecuteCoreAsync(
        InterviewBlackboard blackboard,
        IEnumerable<string> relevantMemories,
        CancellationToken cancellationToken);

    /// <summary>
    /// Helper to validate and post agent output to the blackboard with guardrails applied.
    /// </summary>
    protected void PostGuardedOutput(InterviewBlackboard blackboard, string output)
    {
        var result = Guardrails.ValidateOutput(Name, output, blackboard.SessionId.ToString());
        if (!result.IsAccepted)
        {
            Logger.LogWarning("Agent {AgentName} output rejected by guardrails: {Reason}", Name, result.RejectionReason);
            return;
        }

        var sanitizedOutput = result.SanitizedContent;
        if (!string.IsNullOrWhiteSpace(sanitizedOutput))
        {
            var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
            blackboard.Set(AgenticConstants.CurrentTranscriptKey, currentTranscript + $"\n[{Name}]: {sanitizedOutput}");
            blackboard.AddMessage(new BlackboardMessage(Name, sanitizedOutput, DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Self-correcting variant of <see cref="PostGuardedOutput"/> that re-prompts the LLM
    /// when guardrails reject the output. Instead of silently dropping rejected output,
    /// this method generates corrective feedback and retries up to <see cref="AgenticConstants.MaxSelfCorrectionAttempts"/> times.
    /// 
    /// Agents should call this instead of <see cref="PostGuardedOutput"/> when they want
    /// self-healing behavior on guardrail rejections.
    /// </summary>
    /// <param name="blackboard">The interview blackboard.</param>
    /// <param name="initialOutput">The initial LLM output to validate.</param>
    /// <param name="regenerate">
    /// An async function that takes corrective feedback and returns a new LLM output.
    /// Called on each retry with specific instructions about what to fix.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task PostGuardedOutputWithCorrectionAsync(
        InterviewBlackboard blackboard,
        string initialOutput,
        Func<string, CancellationToken, Task<string>> regenerate,
        CancellationToken cancellationToken = default)
    {
        var sessionId = blackboard.SessionId.ToString();
        var finalOutput = await SelfCorrectingLoop.ExecuteAsync(
            action: async ctx =>
            {
                if (ctx.IsFirstAttempt)
                    return initialOutput;

                // Re-prompt the LLM with corrective feedback
                Logger.LogInformation(
                    "Agent {AgentName} re-prompting (attempt {Attempt}/{Max}) with correction: {Feedback}",
                    Name, ctx.AttemptNumber, ctx.MaxAttempts, ctx.CorrectiveFeedback);

                return await regenerate(ctx.CorrectiveFeedback!, cancellationToken);
            },
            validator: (output, _) =>
            {
                if (string.IsNullOrWhiteSpace(output))
                    return SelfCorrectionValidationResult.Invalid("Output was empty or whitespace.");

                var guardrailResult = Guardrails.ValidateOutput(Name, output, sessionId);
                if (!guardrailResult.IsAccepted)
                    return SelfCorrectionValidationResult.Invalid(
                        $"Guardrail rejection: {guardrailResult.RejectionReason}");

                return SelfCorrectionValidationResult.Valid();
            },
            feedbackGenerator: (output, validationResult, _) =>
            {
                return $"Your previous output was rejected. Reason: {validationResult.FailureReason}\n" +
                       $"Rejected output (DO NOT repeat this): \"{(output.Length > 200 ? output[..200] + "..." : output)}\"\n" +
                       "Please regenerate your response while avoiding the issue described above.";
            },
            options: new SelfCorrectionOptions
            {
                MaxAttempts = AgenticConstants.MaxSelfCorrectionAttempts,
                RetryDelayMs = 500,
                AgentName = Name,
                SessionId = sessionId
            },
            Logger,
            cancellationToken);

        // Post the final output (may still be invalid if all retries exhausted — PostGuardedOutput handles that)
        PostGuardedOutput(blackboard, finalOutput);
    }

    /// <summary>
    /// Delegates a sub-task to another agent and returns its result.
    /// This is a convenience wrapper around <see cref="SubAgentDelegator.DelegateAsync"/>.
    /// 
    /// The sub-agent executes within the current agent's orchestration cycle.
    /// Its output is returned to this agent for incorporation into its own reasoning,
    /// and is NOT automatically posted to the blackboard unless the sub-agent itself does so.
    /// </summary>
    /// <param name="targetAgentName">The name of the sub-agent to delegate to.</param>
    /// <param name="taskPrompt">A description of the task for the sub-agent.</param>
    /// <param name="blackboard">The interview blackboard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SubAgentResult"/> with the sub-agent's output, or a failed result if no delegator is available.</returns>
    protected async Task<SubAgentResult> DelegateToSubAgentAsync(
        string targetAgentName,
        string taskPrompt,
        InterviewBlackboard blackboard,
        CancellationToken cancellationToken = default)
    {
        if (Delegator == null)
        {
            Logger.LogWarning(
                "Agent '{AgentName}' attempted to delegate to '{TargetAgent}' but no SubAgentDelegator is available. " +
                "This typically means the agent was constructed outside of DI (e.g., in a unit test).",
                Name, targetAgentName);

            return SubAgentResult.Failed(
                targetAgentName,
                "SubAgentDelegator not available. Delegation requires DI-managed agent construction.",
                0);
        }

        Logger.LogInformation(
            "Agent '{AgentName}' delegating sub-task to '{TargetAgent}': {Task}",
            Name, targetAgentName,
            taskPrompt.Length > 100 ? taskPrompt[..100] + "..." : taskPrompt);

        return await Delegator.DelegateAsync(
            parentAgentName: Name,
            targetAgentName: targetAgentName,
            taskPrompt: taskPrompt,
            blackboard: blackboard,
            parentContext: null, // Top-level agents are at depth 0
            cancellationToken: cancellationToken);
    }
}

