namespace AgenticInterview.AgenticSystem.Common;

/// <summary>
/// Constants used across the Agentic System for blackboard keys, agent names, and protocol identifiers.
/// Centralizing these prevents magic strings and improves maintainability.
/// </summary>
public static class AgenticConstants
{
    // --- Blackboard Keys ---
    public const string CurrentTranscriptKey = "CurrentTranscript";
    public const string CurrentQuestionKey = "CurrentQuestion";
    public const string CandidateCodeKey = "CurrentCodeSnapshot";
    public const string SessionStatusKey = "SessionStatus";
    public const string DifficultyLevelKey = "DifficultyLevel";
    public const string CandidateScoreKey = "CandidateScore";
    public const string ProctoringStrikeCountKey = "ProctoringStrikeCount";
    public const string CandidateResumeTextKey = "CandidateResume";
    public const string CandidateNameKey = "CandidateName";
    public const string JobDescriptionKey = "JobDescription";
    public const string InterviewPlanKey = "InterviewPlan";
    public const string HrSummaryKey = "HrSummary";
    public const string CandidateJoinedKey = "CandidateJoined";
    public const string PendingMalpracticeKey = "PendingMalpractice";
    public const string CurrentGoalIdKey = "CurrentGoalId";

    // --- Agent Source Names (used in BlackboardMessage.SourceAgent) ---
    public const string TechnicalInterviewerName = "Technical Interviewer";
    public const string BehavioralInterviewerName = "Behavioral Interviewer";
    public const string CodeExecutionAgentName = "Code Execution";
    public const string ProctoringAgentName = "Proctor";
    public const string EvaluationAgentName = "Evaluator";
    public const string ModeratorAgentName = "Moderator";
    public const string HrObserverAgentName = "HR Observer";
    public const string WebSearchAgentName = "Web Searcher";
    public const string CandidateSourceName = "Candidate";
    public const string SystemSourceName = "SYSTEM";

    // --- Protocol Identifiers ---
    public const string McpProtocolVersion = "2025-03-26";
    public const string A2AProtocolVersion = "1.0";
    public const string AgUiProtocolVersion = "1.0";

    // --- Limits ---
    public const int MaxBlackboardMessages = 1000;
    public const int MaxAgentRetries = 3;
    public const int DefaultInterviewDurationMinutes = 60;
    public const int MaxProctoringStrikes = 3;

    // --- Session Statuses ---
    public const string StatusCompleted = "Completed";
    public const string StatusTerminated = "Terminated";
    public const string StatusInProgress = "InProgress";
}
