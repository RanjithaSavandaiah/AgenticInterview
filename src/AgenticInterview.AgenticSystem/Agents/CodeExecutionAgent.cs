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

public class CodeExecutionAgent : BaseAgent
{
    public CodeExecutionAgent(
        IChatClient chatClient,
        ILogger<CodeExecutionAgent> logger,
        IList<AITool> tools,
        IConversationMemoryStore memoryStore,
        AgentGuardrails guardrails,
        AgenticInterview.AgenticSystem.Core.AgentToolResolver toolResolver,
        System.IServiceProvider serviceProvider) 
        : base(AgenticConstants.CodeExecutionAgentName, "Validates code correctness statically via LLM.", chatClient, logger, tools, memoryStore, guardrails, toolResolver, serviceProvider)
    {
    }

    protected override async Task ExecuteCoreAsync(InterviewBlackboard blackboard, IEnumerable<string> relevantMemories, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Code Execution agent executing static analysis.");
        
        var currentCode = blackboard.Get<string>(AgenticConstants.CandidateCodeKey) ?? string.Empty;
        var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        var lastLength = blackboard.Get<int>($"{Name}_LastTranscriptLength");
        
        if (currentTranscript.Length <= lastLength)
        {
            return; // No new information to process
        }
        
        blackboard.Set($"{Name}_LastTranscriptLength", currentTranscript.Length);

        if (string.IsNullOrWhiteSpace(currentCode))
        {
            Logger.LogInformation("No code snapshot available to evaluate.");
            return;
        }

        var userPrompt = $"You are a senior engineer evaluating a candidate's code submission. Do NOT run the code. Perform a static analysis for correctness, time complexity, and edge cases. Provide a score from 0-10 and a brief explanation. You have access to tools. If you decide to use a tool, ensure you provide all required parameters according to the schema.\n\nHere is the code:\n\n{currentCode}";

        // Use the MAF Agent Harness
        var response = await HarnessAgent.RunAsync(userPrompt, cancellationToken: cancellationToken);
        var evaluationResult = response.Text;
        
        Logger.LogInformation("Code evaluation result: {Result}", evaluationResult);
        
        // Use guardrailed output posting
        if (!string.IsNullOrWhiteSpace(evaluationResult))
        {
            var result = Guardrails.ValidateOutput(Name, evaluationResult, blackboard.SessionId.ToString());
            if (result.IsAccepted)
            {
                blackboard.Set(AgenticConstants.CurrentTranscriptKey, currentTranscript + $"\n[AI Code Evaluator]: {result.SanitizedContent}");
            }
        }
    }
}
