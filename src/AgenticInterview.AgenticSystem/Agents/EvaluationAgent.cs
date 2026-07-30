using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Guardrails;
using AgenticInterview.AgenticSystem.Memory;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

#pragma warning disable MAAI001 // Experimental MAF Agent Harness APIs

namespace AgenticInterview.AgenticSystem.Agents;

public class EvaluationAgent : BaseAgent
{
    public EvaluationAgent(
        IChatClient chatClient,
        ILogger<EvaluationAgent> logger,
        IList<AITool> tools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgenticInterview.AgenticSystem.Core.AgentToolResolver toolResolver,
        System.IServiceProvider serviceProvider) 
        : base(AgenticConstants.EvaluationAgentName, "Scores answers, generates final report.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider)
    {
    }

    protected override async Task ExecuteCoreAsync(InterviewBlackboard blackboard, IEnumerable<string> relevantMemories, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Evaluation agent executing.");
        
        var transcript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        var lastLength = blackboard.Get<int>($"{Name}_LastTranscriptLength");
        
        if (transcript.Length <= lastLength)
        {
            return; // No new information to process
        }
        
        var messagesLog = blackboard.GetMessages();
        var lastMessage = System.Linq.Enumerable.LastOrDefault(messagesLog);
        if (lastMessage == null || lastMessage.SourceAgent != AgenticConstants.CandidateSourceName)
        {
            return;
        }
        
        blackboard.Set($"{Name}_LastTranscriptLength", transcript.Length);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            Logger.LogWarning("No transcript in blackboard to evaluate.");
            return;
        }

        var userPrompt = @$"You are the Evaluation Agent. Your session ID is '{blackboard.SessionId}'.
Review the complete transcript of the interview.
Score the candidate based on technical accuracy and behavioral responses.
Use the 'submit_final_score' tool to officially submit the final score.
You MUST pass the exact parameters to the tool: 'sessionId' (which is '{blackboard.SessionId}'), 'technicalScore' (integer 0-100), 'behavioralScore' (integer 0-100), and 'recommendation' (a brief string).
Do NOT output the scores in your text response. JUST call the tool.

Transcript:
{transcript}";

        // Use the MAF Agent Harness — it handles the multi-turn tool-calling loop for submit_final_score
        var response = await HarnessAgent.RunAsync(userPrompt, cancellationToken: cancellationToken);
        var responseText = response.Text ?? string.Empty;
        
        Logger.LogInformation("Evaluation result:\n{Result}", responseText);
    }
}
