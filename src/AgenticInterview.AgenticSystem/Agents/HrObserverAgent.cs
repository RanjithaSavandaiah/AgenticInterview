using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Guardrails;
using AgenticInterview.AgenticSystem.Memory;
using AgenticInterview.AgenticSystem.State;
using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

#pragma warning disable MAAI001 // Experimental MAF Agent Harness APIs

namespace AgenticInterview.AgenticSystem.Agents;

public class HrObserverAgent : BaseAgent
{
    public HrObserverAgent(
        IChatClient chatClient,
        ILogger<HrObserverAgent> logger,
        IList<AITool> tools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgenticInterview.AgenticSystem.Core.AgentToolResolver toolResolver,
        System.IServiceProvider serviceProvider) 
        : base(AgenticConstants.HrObserverAgentName, "Summarizes the interview for the HR view in real-time.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider)
    {
    }

    /// <summary>
    /// Override harness options with a smaller context window.
    /// The HR Observer only generates short summaries — it doesn't need 128K tokens of reasoning capacity.
    /// This is a deliberate model-cascading / token optimization decision.
    /// </summary>
    protected override ChatClientAgentOptions GetAgentOptions()
    {
        return new ChatClientAgentOptions
        {
            Name = Name,
            Description = Goal,
            ChatOptions = new ChatOptions
            {
                Tools = Tools,
                // Instruction Merging: harness merges its reasoning layer with these instructions
                Instructions = @"You are the HR Observer Agent.
Analyze the transcript and provide a short, 2-sentence summary of the candidate's soft skills, communication, and overall demeanor so far.
Do not evaluate technical accuracy. Focus only on HR-related traits.
YOUR ENTIRE RESPONSE MUST BE ONLY THE SUMMARY. DO NOT output any internal monologue, reasoning, or conversational filler like ""Based on the transcript..."".
You have access to tools. If you decide to use a tool, ensure you provide all required parameters according to the schema."
            },
            AIContextProviders = [new CompactionProvider(GetCompactionStrategy())]
        };
    }

    /// <summary>
    /// Override compaction with a much lower 12K token threshold.
    /// The HR observer only needs a summary of the full transcript — not deep reasoning.
    /// Triggering compaction early saves tokens for this lightweight agent.
    /// </summary>
    protected override CompactionStrategy GetCompactionStrategy()
    {
        return new SummarizationCompactionStrategy(
            chatClient: ChatClient,
            trigger: CompactionTriggers.TokensExceed(12_000),
            minimumPreservedGroups: 1
        );
    }

    protected override async Task ExecuteCoreAsync(InterviewBlackboard blackboard, IEnumerable<string> relevantMemories, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("HR Observer agent executing.");
        var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        var lastLength = blackboard.Get<int>($"{Name}_LastTranscriptLength");
        
        if (currentTranscript.Length <= lastLength)
        {
            return; // No new information to process
        }
        
        var messagesLog = blackboard.GetMessages();
        var lastMessage = System.Linq.Enumerable.LastOrDefault(messagesLog);
        if (lastMessage == null || lastMessage.SourceAgent != AgenticConstants.CandidateSourceName)
        {
            return;
        }
        
        blackboard.Set($"{Name}_LastTranscriptLength", currentTranscript.Length);

        if (string.IsNullOrWhiteSpace(currentTranscript))
            return;

        // Use the MAF Agent Harness — instruction merging handles the system prompt,
        // context compaction handles transcript overflow automatically
        var response = await HarnessAgent.RunAsync(
            $"Transcript so far:\n{currentTranscript}",
            cancellationToken: cancellationToken);
        
        var summary = response.Text ?? string.Empty;
        
        Logger.LogInformation("HR Summary updated: {Summary}", summary);
        blackboard.Set(AgenticConstants.HrSummaryKey, summary);
    }
}
