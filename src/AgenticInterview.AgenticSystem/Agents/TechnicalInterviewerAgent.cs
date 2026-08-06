using System.Collections.Generic;
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

public class TechnicalInterviewerAgent : BaseAgent
{
    public TechnicalInterviewerAgent(
        IChatClient chatClient,
        ILogger<TechnicalInterviewerAgent> logger,
        IList<AITool> tools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgenticInterview.AgenticSystem.Core.AgentToolResolver toolResolver,
        System.IServiceProvider serviceProvider,
        AgenticInterview.AgenticSystem.Core.SubAgentDelegator delegator) 
        : base(AgenticConstants.TechnicalInterviewerName, "Asks coding/system design questions, generates challenges.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider, delegator)
    {
    }

    /// <summary>
    /// Override harness options to use full context window for deep technical reasoning
    /// and merge the base interviewer instructions via the harness instruction merging system.
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
                // Instruction Merging: the harness combines its reasoning instructions with these
                Instructions = @"You are an expert Technical Interviewer speaking directly to the candidate.
CRITICAL RULE 1: You are roleplaying as the interviewer. You MUST ONLY output the exact spoken dialogue you want to say out loud to the candidate. Your entire response will be spoken by Text-to-Speech.
CRITICAL RULE 2: You MUST ALWAYS ask a question. NEVER answer technical concepts yourself. If the candidate introduces themselves, acknowledge it briefly and immediately ask a technical question."
            }
        };
    }

    protected override async Task ExecuteCoreAsync(InterviewBlackboard blackboard, IEnumerable<string> relevantMemories, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Technical Interviewer agent executing.");
        
        var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        var candidateResume = blackboard.Get<string>(AgenticConstants.CandidateResumeTextKey) ?? string.Empty;
        var jobDescription = blackboard.Get<string>(AgenticConstants.JobDescriptionKey) ?? string.Empty;
        var candidateName = blackboard.Get<string>(AgenticConstants.CandidateNameKey) ?? "Candidate";

        var messagesLog = blackboard.GetMessages();
        var lastMessage = System.Linq.Enumerable.LastOrDefault(messagesLog);
        
        // Wait until candidate has fully joined the live session
        if (!blackboard.Get<bool>(AgenticConstants.CandidateJoinedKey))
        {
            return;
        }

        // Wait for candidate to respond before asking another question
        if (lastMessage != null && lastMessage.SourceAgent != AgenticConstants.CandidateSourceName)
        {
            return;
        }

        // Do not generate a new interview question if a malpractice warning was just issued
        if (lastMessage != null && lastMessage.SourceAgent == AgenticConstants.ProctoringAgentName)
        {
            return;
        }
        if (!string.IsNullOrEmpty(blackboard.Get<string>(AgenticConstants.PendingMalpracticeKey)))
        {
            return;
        }

        var lastAttempt = blackboard.Get<System.DateTime>($"{Name}_LastAttempt");
        if (System.DateTime.UtcNow - lastAttempt < System.TimeSpan.FromSeconds(5))
        {
            return; // Cooldown to prevent API rate-limit death spirals
        }
        blackboard.Set($"{Name}_LastAttempt", System.DateTime.UtcNow);

        // Build memory context from previous interactions
        var memoryContext = string.Join("\n", relevantMemories);

        // --- Sub-Agent Delegation: Code Review ---
        // If the candidate has submitted code, delegate to CodeExecution for static analysis
        // and incorporate the feedback into the next question prompt.
        var codeReviewContext = string.Empty;
        var candidateCode = blackboard.Get<string>(AgenticConstants.CandidateCodeKey) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(candidateCode))
        {
            var lastCodeReviewHash = blackboard.Get<string>($"{Name}_LastCodeReviewHash") ?? string.Empty;
            var currentCodeHash = candidateCode.GetHashCode().ToString();

            // Only delegate if the code has changed since the last review
            if (currentCodeHash != lastCodeReviewHash)
            {
                Logger.LogInformation("Candidate code detected — delegating to Code Execution sub-agent for review.");

                var codeReviewResult = await DelegateToSubAgentAsync(
                    AgenticConstants.CodeExecutionAgentName,
                    $"Analyze the following candidate-submitted code for correctness, quality, and potential issues. " +
                    $"Provide a brief summary suitable for the interviewer to reference when asking a follow-up question.\n\n" +
                    $"Code:\n```\n{candidateCode}\n```",
                    blackboard,
                    cancellationToken);

                if (codeReviewResult.Success && !string.IsNullOrWhiteSpace(codeReviewResult.Output))
                {
                    codeReviewContext = $"\n\nCode Review Feedback (from Code Execution sub-agent, {codeReviewResult.DurationMs:F0}ms):\n{codeReviewResult.Output}";
                    Logger.LogInformation("Code review delegation succeeded in {DurationMs}ms.", codeReviewResult.DurationMs);
                }
                else if (!codeReviewResult.Success)
                {
                    Logger.LogWarning("Code review delegation failed: {Error}. Proceeding without code feedback.", codeReviewResult.ErrorMessage);
                }

                blackboard.Set($"{Name}_LastCodeReviewHash", currentCodeHash);
            }
        }

        // Build the user prompt with dynamic context — the harness handles instruction merging
        // with the system prompt defined in GetHarnessOptions()
        var userPrompt = string.IsNullOrWhiteSpace(currentTranscript)
            ? $"This is the very beginning of the interview. Greet {candidateName} by name, introduce yourself as the AI Interviewer, and ask an introductory question (e.g., 'Tell me about yourself and your background.')."
            : $"JD: {jobDescription}\nResume: {candidateResume}\n{(string.IsNullOrWhiteSpace(memoryContext) ? "" : $"\nRelevant context from previous interactions:\n{memoryContext}")}{codeReviewContext}\n\nTranscript so far:\n{currentTranscript}";

        // Self-correcting loop: validates the LLM output is a proper interview question
        var nextQuestion = await AgenticInterview.AgenticSystem.Core.SelfCorrectingLoop.ExecuteAsync(
            action: async ctx =>
            {
                var prompt = ctx.IsFirstAttempt
                    ? userPrompt
                    : $"{userPrompt}\n\n--- CORRECTION REQUIRED ---\n{ctx.CorrectiveFeedback}";

                var response = await HarnessAgent.RunAsync(prompt, cancellationToken: cancellationToken);
                return response.Text?.Trim() ?? string.Empty;
            },
            validator: (output, _) =>
            {
                if (string.IsNullOrWhiteSpace(output) || output == "[Could not generate question]")
                    return AgenticInterview.AgenticSystem.Core.SelfCorrectionValidationResult.Invalid(
                        "Output was empty or the degenerate fallback '[Could not generate question]'.");

                if (!output.Contains('?'))
                    return AgenticInterview.AgenticSystem.Core.SelfCorrectionValidationResult.Invalid(
                        "Output does not contain a question mark. You MUST ask the candidate a question.");

                return AgenticInterview.AgenticSystem.Core.SelfCorrectionValidationResult.Valid();
            },
            feedbackGenerator: (output, validationResult, _) =>
            {
                return $"Your previous output was not a valid interview question. Issue: {validationResult.FailureReason}\n" +
                       $"Invalid output: \"{(output.Length > 150 ? output[..150] + "..." : output)}\"\n" +
                       "You MUST ask the candidate a clear, specific interview question. Your response must contain at least one question mark.";
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

        Logger.LogInformation("Next technical question chosen: {Question}", nextQuestion);
        
        // Use guardrailed output posting with self-correction
        await PostGuardedOutputWithCorrectionAsync(
            blackboard,
            nextQuestion,
            async (feedback, ct) =>
            {
                var correctionPrompt = $"{userPrompt}\n\n--- GUARDRAIL CORRECTION ---\n{feedback}";
                var response = await HarnessAgent.RunAsync(correctionPrompt, cancellationToken: ct);
                return response.Text?.Trim() ?? string.Empty;
            },
            cancellationToken);
    }
}

