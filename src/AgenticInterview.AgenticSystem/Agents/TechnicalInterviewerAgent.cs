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
        System.IServiceProvider serviceProvider) 
        : base(AgenticConstants.TechnicalInterviewerName, "Asks coding/system design questions, generates challenges.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider)
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

        // Build the user prompt with dynamic context — the harness handles instruction merging
        // with the system prompt defined in GetHarnessOptions()
        var userPrompt = string.IsNullOrWhiteSpace(currentTranscript)
            ? $"This is the very beginning of the interview. Greet {candidateName} by name, introduce yourself as the AI Interviewer, and ask an introductory question (e.g., 'Tell me about yourself and your background.')."
            : $"JD: {jobDescription}\nResume: {candidateResume}\n{(string.IsNullOrWhiteSpace(memoryContext) ? "" : $"\nRelevant context from previous interactions:\n{memoryContext}")}\n\nTranscript so far:\n{currentTranscript}";

        // Use the MAF Agent Harness — automatic context compaction and tool orchestration
        var response = await HarnessAgent.RunAsync(userPrompt, cancellationToken: cancellationToken);
        var nextQuestion = response.Text?.Trim() ?? "[Could not generate question]";
        
        Logger.LogInformation("Next technical question chosen: {Question}", nextQuestion);
        
        // Use guardrailed output posting
        PostGuardedOutput(blackboard, nextQuestion);
    }
}
