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

        // Self-correcting loop: validates that the tool was actually invoked
        // The LLM sometimes outputs scores as plain text instead of calling the tool
        await AgenticInterview.AgenticSystem.Core.SelfCorrectingLoop.ExecuteAsync(
            action: async ctx =>
            {
                var prompt = ctx.IsFirstAttempt
                    ? userPrompt
                    : $"{userPrompt}\n\n--- CORRECTION REQUIRED ---\n{ctx.CorrectiveFeedback}";

                var response = await HarnessAgent.RunAsync(prompt, cancellationToken: cancellationToken);
                return response.Text ?? string.Empty;
            },
            validator: (responseText, _) =>
            {
                // If the response contains score patterns in plain text, the LLM likely
                // skipped tool use and just wrote the scores as text
                var hasPlainTextScores = System.Text.RegularExpressions.Regex.IsMatch(
                    responseText,
                    @"(technical\s*score|behavioral\s*score|score\s*:\s*\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (hasPlainTextScores && responseText.Length > 50)
                {
                    return AgenticInterview.AgenticSystem.Core.SelfCorrectionValidationResult.Invalid(
                        "You output scores as plain text instead of calling the 'submit_final_score' tool. " +
                        "You MUST call the tool — do NOT write scores in your response text.");
                }

                return AgenticInterview.AgenticSystem.Core.SelfCorrectionValidationResult.Valid();
            },
            feedbackGenerator: (_, validationResult, _) =>
            {
                return $"CRITICAL: {validationResult.FailureReason}\n" +
                       "You MUST use the 'submit_final_score' tool with parameters: " +
                       $"sessionId='{blackboard.SessionId}', technicalScore (0-100), behavioralScore (0-100), recommendation (string).\n" +
                       "Call the tool NOW. Do NOT output any scores as text.";
            },
            options: new AgenticInterview.AgenticSystem.Core.SelfCorrectionOptions
            {
                MaxAttempts = AgenticConstants.MaxSelfCorrectionAttempts,
                RetryDelayMs = 500,
                AgentName = Name,
                SessionId = blackboard.SessionId.ToString()
            },
            Logger,
            cancellationToken);

        Logger.LogInformation("Evaluation agent completed with self-correction.");
    }
}

