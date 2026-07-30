using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Guardrails;
using AgenticInterview.AgenticSystem.Memory;
using AgenticInterview.AgenticSystem.State;
using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

#pragma warning disable MAAI001 // Experimental MAF Agent Harness APIs

namespace AgenticInterview.AgenticSystem.Agents;

public class WebSearchAgent : BaseAgent
{
    public WebSearchAgent(
        IChatClient chatClient,
        ILogger<WebSearchAgent> logger,
        IList<AITool> tools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgenticInterview.AgenticSystem.Core.AgentToolResolver toolResolver,
        System.IServiceProvider serviceProvider) 
        : base(AgenticConstants.WebSearchAgentName, "Fact-checks candidate answers if highly specialized.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider)
    {
    }

    protected override async Task ExecuteCoreAsync(InterviewBlackboard blackboard, IEnumerable<string> relevantMemories, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Web Search agent executing.");
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

        var userPrompt = @$"You are a Fact-Checking Web Search Agent.
Review the latest answers from the candidate in the transcript.
If they made highly specific, dubious, or specialized technical claims that need verification, use the 'search_web' tool to perform a web search to verify the claim.
After verifying, output a brief fact-check note.
If no dubious claims are present, or no search is needed, output 'NO_SEARCH_NEEDED'.

Transcript so far:
{currentTranscript}";

        // Use the MAF Agent Harness — it handles the multi-turn search_web tool call loop
        var response = await HarnessAgent.RunAsync(userPrompt, cancellationToken: cancellationToken);
        
        var factCheckResult = response.Text ?? string.Empty;
        
        if (factCheckResult.Trim().ToUpperInvariant() != "NO_SEARCH_NEEDED")
        {
            Logger.LogInformation("Web Search agent fact-checked: {Result}", factCheckResult);
            PostGuardedOutput(blackboard, $"[Fact-Check]: {factCheckResult}");
        }
    }
}
