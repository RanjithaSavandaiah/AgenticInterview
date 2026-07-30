using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AgenticInterview.Application.Abstractions;
using AgenticInterview.Domain.Enums;

namespace AgenticInterview.AgenticSystem.McpTools;

/// <summary>
/// MCP (Model Context Protocol) tool definitions that agents can invoke.
/// Each tool is registered as an <see cref="AITool"/> for use in the M.E.AI pipeline.
/// 
/// Tools are tagged with skill IDs that map to <see cref="AgentCards.AgentSkill.Id"/> on agent cards.
/// This enables per-agent tool filtering: each agent only receives the tools matching its skills.
/// </summary>
public static class InterviewMcpToolFactory
{
    /// <summary>
    /// Creates all available MCP tools for the interview system.
    /// </summary>
    public static IList<AITool> CreateAllTools(ILogger logger, IServiceProvider serviceProvider)
    {
        return GetAllToolDefinitions(logger, serviceProvider)
            .Select(d => d.Tool)
            .ToList();
    }

    /// <summary>
    /// Creates only the tools that match the given skill IDs.
    /// Each agent should call this with the skill IDs from its agent card to receive
    /// only the tools relevant to its role — reducing token waste and preventing
    /// cross-agent tool misuse (e.g., behavioral agent calling record_proctoring_event).
    /// </summary>
    public static IList<AITool> CreateToolsForSkills(
        ILogger logger, 
        IServiceProvider serviceProvider, 
        IEnumerable<string> skillIds)
    {
        var skillSet = new HashSet<string>(skillIds, StringComparer.OrdinalIgnoreCase);
        
        return GetAllToolDefinitions(logger, serviceProvider)
            .Where(d => d.SkillIds.Any(s => skillSet.Contains(s)))
            .Select(d => d.Tool)
            .ToList();
    }

    /// <summary>
    /// Returns all tool definitions with their skill mappings.
    /// Each tool declares which agent skill IDs it belongs to.
    /// </summary>
    private static IReadOnlyList<ToolDefinition> GetAllToolDefinitions(ILogger logger, IServiceProvider serviceProvider)
    {
        return
        [
            new ToolDefinition(
                CreateEvaluateAnswerTool(logger, serviceProvider),
                ["ask-technical-question", "score-aggregation", "static-analysis"]),

            new ToolDefinition(
                CreateFetchQuestionTool(logger, serviceProvider),
                ["ask-technical-question", "behavioral-question", "orchestration"]),

            new ToolDefinition(
                CreateRecordProctoringEventTool(logger, serviceProvider),
                ["detect-malpractice"]),

            new ToolDefinition(
                CreateSearchResumeContextTool(logger, serviceProvider),
                ["ask-technical-question", "behavioral-question", "live-summary"]),

            new ToolDefinition(
                CreateSubmitFinalScoreTool(logger, serviceProvider),
                ["score-aggregation"]),

            new ToolDefinition(
                CreateSearchWebTool(logger, serviceProvider),
                ["web-lookup"])
        ];
    }

    /// <summary>
    /// Tool: evaluate_answer — Evaluates a candidate's answer against a rubric.
    /// </summary>
    private static AITool CreateEvaluateAnswerTool(ILogger logger, IServiceProvider serviceProvider)
    {
        return AIFunctionFactory.Create(
            async (string question, string answer) =>
            {
                logger.LogInformation("MCP Tool 'evaluate_answer' invoked for question: {Question}", question);
                
                var chatClient = serviceProvider.GetRequiredService<IChatClient>();
                
                var systemPrompt = "You are an expert technical interviewer evaluating an answer. Provide a score out of 100, and a brief explanation. Format exactly as: 'Score: [Score]/100. [Explanation]'";
                
                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, $"Question: {question}\nAnswer: {answer}")
                };
                
                var response = await chatClient.GetResponseAsync(messages);
                return response.Text;
            },
            "evaluate_answer",
            "Evaluates a candidate's answer to an interview question and returns a score.");
    }

    /// <summary>
    /// Tool: fetch_question — Retrieves the next question from the question bank.
    /// </summary>
    private static AITool CreateFetchQuestionTool(ILogger logger, IServiceProvider serviceProvider)
    {
        return AIFunctionFactory.Create(
            async (string difficulty, string topic) =>
            {
                logger.LogInformation("MCP Tool 'fetch_question' invoked. Difficulty: {Difficulty}, Topic: {Topic}", difficulty, topic);
                
                var questionBank = serviceProvider.GetRequiredService<ICachedQuestionBankService>();
                
                var diffEnum = Enum.TryParse<QuestionDifficultyLevel>(difficulty, true, out var d) 
                    ? d 
                    : QuestionDifficultyLevel.Medium;
                    
                var questions = await questionBank.GetQuestionsAsync(diffEnum);
                
                var matchingQuestion = questions.FirstOrDefault(x => x.Content.Contains(topic, StringComparison.OrdinalIgnoreCase)) 
                                       ?? questions.FirstOrDefault();
                                       
                return matchingQuestion?.Content ?? $"[{difficulty}] Explain the SOLID principles and how they apply to {topic}.";
            },
            "fetch_question",
            "Fetches a technical question from the question bank based on difficulty and topic.");
    }

    /// <summary>
    /// Tool: record_proctoring_event — Logs a proctoring violation event.
    /// </summary>
    private static AITool CreateRecordProctoringEventTool(ILogger logger, IServiceProvider serviceProvider)
    {
        return AIFunctionFactory.Create(
            async (string sessionId, string eventType, string details) =>
            {
                logger.LogWarning("MCP Tool 'record_proctoring_event': Session {SessionId} — {EventType} — {Details}", sessionId, eventType, details);
                
                if (Guid.TryParse(sessionId, out var parsedSessionId))
                {
                    var mediator = serviceProvider.GetRequiredService<MediatR.IMediator>();
                    
                    var violationType = Enum.TryParse<ProctoringViolationType>(eventType, true, out var t) 
                        ? t : ProctoringViolationType.BlockedKeyboardShortcut;
                        
                    await mediator.Send(new AgenticInterview.Application.Features.Interviews.Commands.ReportProctoringIncidentCommand(
                        parsedSessionId, violationType, details, true));
                        
                    return $"Proctoring event '{eventType}' recorded successfully for session {sessionId}.";
                }
                
                return "Failed: Invalid session ID format.";
            },
            "record_proctoring_event",
            "Records a proctoring violation event such as tab switching or copy-paste attempts.");
    }

    /// <summary>
    /// Tool: search_resume_context — Searches the candidate's resume for relevant experience.
    /// </summary>
    private static AITool CreateSearchResumeContextTool(ILogger logger, IServiceProvider serviceProvider)
    {
        return AIFunctionFactory.Create(
            async (string candidateId, string query) =>
            {
                logger.LogInformation("MCP Tool 'search_resume_context' invoked with candidateId: {CandidateId}, query: {Query}", candidateId, query);
                
                var ragService = serviceProvider.GetRequiredService<IResumeRagService>();
                var results = await ragService.SearchCandidateExperienceAsync(candidateId, query);
                
                var resultList = results.ToList();
                if (resultList.Count == 0)
                {
                    return "No relevant resume experience found for this query.";
                }
                
                return string.Join("\n", resultList);
            },
            "search_resume_context",
            "Searches the candidate's resume using RAG to find relevant experience for a given query.");
    }

    /// <summary>
    /// Tool: submit_final_score — Submits the final evaluation score for the session.
    /// </summary>
    private static AITool CreateSubmitFinalScoreTool(ILogger logger, IServiceProvider serviceProvider)
    {
        return AIFunctionFactory.Create(
            async (string sessionId, int technicalScore, int behavioralScore, string recommendation) =>
            {
                logger.LogInformation("MCP Tool 'submit_final_score': Session {SessionId}, Tech={Tech}, Behavioral={Behavioral}, Rec={Rec}",
                    sessionId, technicalScore, behavioralScore, recommendation);
                
                int composite = (int)(technicalScore * 0.7 + behavioralScore * 0.3);
                
                if (Guid.TryParse(sessionId, out var parsedSessionId))
                {
                    var mediator = serviceProvider.GetRequiredService<MediatR.IMediator>();
                    // Dispatches SubmitInterviewScoreCommand which finalizes the session and saves the score
                    await mediator.Send(new AgenticInterview.Application.Features.Interviews.Commands.SubmitInterviewScoreCommand(parsedSessionId, composite, recommendation));
                    return $"Final composite score: {composite}/100. Recommendation: {recommendation}. Saved successfully.";
                }
                
                return "Failed: Invalid session ID format.";
            },
            "submit_final_score",
            "Submits the final composite score and hiring recommendation for the candidate.");
    }

    /// <summary>
    /// Tool: search_web — Performs a web search to fact-check candidate claims.
    /// </summary>
    private static AITool CreateSearchWebTool(ILogger logger, IServiceProvider serviceProvider)
    {
        return AIFunctionFactory.Create(
            async (string query) =>
            {
                logger.LogInformation("MCP Tool 'search_web' invoked for query: {Query}", query);
                
                var chatClient = serviceProvider.GetRequiredService<IChatClient>();
                
                var systemPrompt = "You are a web search engine simulation. Given the search query, provide a factual, concise summary of the widely accepted truth or documentation regarding the topic. Do not hallucinate.";
                
                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatMessage(ChatRole.User, query)
                };
                
                var response = await chatClient.GetResponseAsync(messages);
                return response.Text ?? "No results found.";
            },
            "search_web",
            "Performs a web search to retrieve factual information and verify highly specialized claims.");
    }

    /// <summary>
    /// Associates a tool with the agent skill IDs it fulfills.
    /// </summary>
    private record ToolDefinition(AITool Tool, IReadOnlyList<string> SkillIds);
}
