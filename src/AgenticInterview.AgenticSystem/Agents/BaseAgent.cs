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

    protected BaseAgent(
        string name,
        string goal,
        IChatClient chatClient,
        ILogger logger,
        IList<AITool> allTools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgentToolResolver? toolResolver = null,
        IServiceProvider? serviceProvider = null)
    {
        Name = name;
        Goal = goal;
        ChatClient = chatClient;
        Logger = logger;
        MemoryStore = memoryStore;
        Guardrails = guardrails;

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
    /// </summary>
    protected virtual ChatClientAgentOptions GetAgentOptions()
    {
        return new ChatClientAgentOptions
        {
            Name = Name,
            Description = Goal,
            ChatOptions = new ChatOptions
            {
                Tools = Tools
            },
            // Wire the compaction provider into the agent pipeline for automatic headroom management.
            // Before every RunAsync call, the CompactionProvider checks if the conversation exceeds
            // the token threshold and applies the compaction strategy (summarize or truncate).
            AIContextProviders = [new CompactionProvider(GetCompactionStrategy())]
        };
    }

    /// <summary>
    /// Returns the compaction strategy for headroom management. Concrete agents can override
    /// this to customize when and how compaction occurs.
    /// 
    /// Default: <see cref="SummarizationCompactionStrategy"/> that triggers when token count
    /// exceeds 100,000 and preserves the last 2 message groups (most recent Q&amp;A pair),
    /// summarizing everything older into a condensed summary message.
    /// </summary>
    protected virtual CompactionStrategy GetCompactionStrategy()
    {
        return new SummarizationCompactionStrategy(
            chatClient: ChatClient,
            trigger: CompactionTriggers.TokensExceed(100_000),
            minimumPreservedGroups: 2
        );
    }

    /// <summary>
    /// Executes the agent with memory retrieval and output guardrails wrapped around the core logic.
    /// </summary>
    public async Task ExecuteAsync(InterviewBlackboard blackboard, CancellationToken cancellationToken = default)
    {
        // Retrieve relevant memories for this agent's context
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
                // Memory retrieval is best-effort enrichment — don't block agent execution
                Logger.LogWarning(ex, "Failed to retrieve memories for agent {AgentName}. Proceeding without context.", Name);
            }
        }

        // Execute the agent's core logic
        await ExecuteCoreAsync(blackboard, relevantMemories, cancellationToken);

        // Save the latest interaction as a memory entry
        var updatedTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        if (updatedTranscript.Length > currentTranscript.Length)
        {
            var newContent = updatedTranscript[currentTranscript.Length..].Trim();
            if (!string.IsNullOrWhiteSpace(newContent) && newContent.Length > 20)
            {
                await MemoryStore.SaveMemoryAsync(sessionId, $"{Name}_{DateTime.UtcNow:HHmmss}", newContent);
            }
        }
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
}
