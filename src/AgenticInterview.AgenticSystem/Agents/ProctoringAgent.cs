using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Guardrails;
using AgenticInterview.AgenticSystem.Memory;
using AgenticInterview.AgenticSystem.State;
using AgenticInterview.Domain.Enums;
using MediatR;
using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

#pragma warning disable MAAI001 // Experimental MAF Agent Harness APIs

namespace AgenticInterview.AgenticSystem.Agents;

public class ProctoringAgent : BaseAgent
{
    private readonly IMediator _mediator;

    public ProctoringAgent(
        IChatClient chatClient,
        ILogger<ProctoringAgent> logger,
        IMediator mediator,
        IList<AITool> tools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgenticInterview.AgenticSystem.Core.AgentToolResolver toolResolver,
        System.IServiceProvider serviceProvider) 
        : base(AgenticConstants.ProctoringAgentName, "Monitors for cheating, analyzes audio for background voices.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Override harness options with a smaller context window.
    /// Proctoring only needs a short transcript snippet to reason about violations.
    /// The harness also handles the multi-turn tool-calling loop for record_proctoring_event automatically.
    /// </summary>
    protected override ChatClientAgentOptions GetAgentOptions()
    {
        return new ChatClientAgentOptions
        {
            Name = Name,
            Description = Goal,
            ChatOptions = new ChatOptions
            {
                Tools = Tools
            },
            // Lightweight compaction — truncate (drop oldest messages) instead of summarize
            AIContextProviders = [new CompactionProvider(GetCompactionStrategy())]
        };
    }

    /// <summary>
    /// Proctoring uses truncation-based compaction (drop oldest messages beyond 5K tokens).
    /// This is cheaper than summarization since the agent only processes short violation snippets.
    /// </summary>
    protected override CompactionStrategy GetCompactionStrategy()
    {
        return new TruncationCompactionStrategy(
            trigger: CompactionTriggers.TokensExceed(5_000)
        );
    }

    protected override async Task ExecuteCoreAsync(InterviewBlackboard blackboard, IEnumerable<string> relevantMemories, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Proctoring agent executing.");
        
        // Check if there's a pending malpractice event set by the Hub fast-path
        var pendingViolation = blackboard.Get<string>(AgenticConstants.PendingMalpracticeKey);
        if (string.IsNullOrEmpty(pendingViolation))
        {
            return; // No violations to analyze
        }

        // Clear the flag immediately so we don't process it again
        blackboard.Set(AgenticConstants.PendingMalpracticeKey, string.Empty);

        var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        var strikeCount = blackboard.Get<int>(AgenticConstants.ProctoringStrikeCountKey);

        Logger.LogWarning("ProctoringAgent analyzing violation: {ViolationType}, Strike: {StrikeCount}", 
            pendingViolation, strikeCount);

        // Build the prompt — the harness handles instruction merging and multi-turn tool calling
        var userPrompt = @$"You are the Proctoring Agent for an AI interview system. Your session ID is '{blackboard.SessionId}'.
A malpractice violation has just been detected:
- Violation Type: {pendingViolation}
- Current Strike Count: {strikeCount} of {AgenticConstants.MaxProctoringStrikes}
- Transcript Context: The last few exchanges give you context about what the candidate was doing when the violation occurred.

Your task:
1. Analyze WHY this violation might have occurred based on the interview context.
2. Use the 'record_proctoring_event' tool to log this incident. Pass these parameters:
   - sessionId: '{blackboard.SessionId}'
   - eventType: '{pendingViolation}'
   - details: Your brief reasoning about why this violation occurred (e.g., 'Candidate switched tabs while being asked about async/await. Possible attempt to look up the answer.')
3. After using the tool, output 'PROCTOR_DONE' and nothing else.

CRITICAL: You MUST use the 'record_proctoring_event' tool. DO NOT output any text other than 'PROCTOR_DONE' after the tool call.

Transcript context (last 500 chars):
{(currentTranscript.Length > 500 ? currentTranscript[^500..] : currentTranscript)}";

        try
        {
            // Use the MAF Agent Harness — it orchestrates the multi-turn tool-calling loop
            // (call LLM → detect tool call → execute tool → feed result back → get final text)
            await HarnessAgent.RunAsync(userPrompt, cancellationToken: cancellationToken);
            Logger.LogInformation("ProctoringAgent completed agentic analysis for violation: {ViolationType}", pendingViolation);
        }
        catch (System.Exception ex)
        {
            // If LLM call fails (e.g., 429 rate limit), the fast-path in the Hub
            // already handled the immediate warning. Log and move on gracefully.
            Logger.LogWarning(ex, "ProctoringAgent LLM analysis failed for violation {ViolationType}. " +
                                  "The fast-path warning was already delivered to the candidate.", pendingViolation);
        }
    }
}
