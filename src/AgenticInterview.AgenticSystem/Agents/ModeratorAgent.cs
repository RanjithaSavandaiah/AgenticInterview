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

public class ModeratorAgent : BaseAgent
{
    public ModeratorAgent(
        IChatClient chatClient,
        ILogger<ModeratorAgent> logger,
        IList<AITool> tools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgenticInterview.AgenticSystem.Core.AgentToolResolver toolResolver,
        System.IServiceProvider serviceProvider) 
        : base(AgenticConstants.ModeratorAgentName, "Orchestrates the interview, keeps track of time, handles introductions.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider)
    {
    }

    protected override ChatClientAgentOptions GetAgentOptions()
    {
        return new ChatClientAgentOptions
        {
            Name = Name,
            Description = Goal,
            ChatOptions = new ChatOptions
            {
                Tools = Tools,
                Instructions = @"You are the Interview Moderator. 
Your job is to manage the flow of the interview. 
If the interview seems to be completely finishing (based on context), generate a polite closing statement.
CRITICAL INSTRUCTION: You MUST output EXACTLY the word 'CONTINUE' and nothing else. DO NOT generate dialogue, DO NOT ask questions, and DO NOT hallucinate the candidate's response!
DO NOT attempt to complete the transcript."
            }
        };
    }

    protected override async Task ExecuteCoreAsync(InterviewBlackboard blackboard, IEnumerable<string> relevantMemories, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Moderator agent executing.");
        // Wait until candidate has fully joined the live session
        if (!blackboard.Get<bool>(AgenticConstants.CandidateJoinedKey))
        {
            return;
        }

        var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        var lastLength = blackboard.Get<int>($"{Name}_LastTranscriptLength");
        
        if (currentTranscript.Length <= lastLength)
        {
            return; // No new information to process
        }

        var userPrompt = $"Transcript so far:\n{(string.IsNullOrWhiteSpace(currentTranscript) ? "(Empty)" : currentTranscript)}\n\nWhat is your next action? (Reply only with 'CONTINUE' unless the interview is ending)";

        // Use the MAF Agent Harness
        var response = await HarnessAgent.RunAsync(userPrompt, cancellationToken: cancellationToken);
        
        var message = response.Text ?? string.Empty;
        
        if (!message.Trim().ToUpperInvariant().Contains("CONTINUE"))
        {
            Logger.LogInformation("Moderator spoke: {Message}", message);
            PostGuardedOutput(blackboard, message);
            blackboard.Set($"{Name}_LastTranscriptLength", (blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty).Length);
        }
        else
        {
            blackboard.Set($"{Name}_LastTranscriptLength", currentTranscript.Length);
        }
    }
}
