using AgenticInterview.AgenticSystem.Common;

namespace AgenticInterview.AgenticSystem.GoalDefinitions;

/// <summary>
/// Defines a high-level interview goal that the orchestrator uses to
/// plan and sequence agent activities. Each goal maps to a phase of the interview.
/// </summary>
public class InterviewGoal
{
    /// <summary>
    /// Unique identifier for this goal.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable name of the goal (e.g., "Technical Assessment Phase").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Detailed description of what this goal aims to achieve.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The agents involved in achieving this goal, referenced by their Agent Card IDs.
    /// </summary>
    public IReadOnlyList<string> RequiredAgentIds { get; init; } = [];

    /// <summary>
    /// Preconditions that must be true on the blackboard before this goal can start.
    /// Keys are blackboard keys, values are the expected values.
    /// </summary>
    public Dictionary<string, string> Preconditions { get; init; } = new();

    /// <summary>
    /// The blackboard key that will be set to "true" when this goal is completed.
    /// </summary>
    public string CompletionKey { get; init; } = string.Empty;

    /// <summary>
    /// The estimated duration for this goal in minutes.
    /// </summary>
    public int EstimatedDurationMinutes { get; init; }
}

/// <summary>
/// Provides a static factory for the default interview goal definitions.
/// </summary>
public static class DefaultInterviewGoals
{
    /// <summary>
    /// Returns the standard set of interview goals that define the interview lifecycle.
    /// </summary>
    public static IReadOnlyList<InterviewGoal> GetAll()
    {
        return new List<InterviewGoal>
        {
            new()
            {
                Id = "goal-intro",
                Name = "Introduction & Warm-Up",
                Description = "Greet the candidate, verify identity, and explain the interview format.",
                RequiredAgentIds = ["moderator", "behavioral-interviewer"],
                CompletionKey = "Goal_IntroComplete",
                EstimatedDurationMinutes = 5
            },
            new()
            {
                Id = "goal-technical",
                Name = "Technical Assessment Phase",
                Description = "Ask progressively challenging coding and system design questions. Evaluate code submissions.",
                RequiredAgentIds = ["technical-interviewer", "code-execution", "proctoring"],
                CompletionKey = "Goal_TechnicalComplete",
                EstimatedDurationMinutes = 30
            },
            new()
            {
                Id = "goal-behavioral",
                Name = "Behavioral Assessment Phase",
                Description = "Evaluate soft skills, communication, and cultural fit using STAR-method questions.",
                RequiredAgentIds = ["behavioral-interviewer", "proctoring"],
                CompletionKey = "Goal_BehavioralComplete",
                EstimatedDurationMinutes = 15
            },
            new()
            {
                Id = "goal-evaluation",
                Name = "Final Evaluation & Scoring",
                Description = "Aggregate all agent assessments into a composite score and generate a recommendation.",
                RequiredAgentIds = ["evaluation", "hr-observer"],
                CompletionKey = "Goal_EvaluationComplete",
                EstimatedDurationMinutes = 5
            },
            new()
            {
                Id = "goal-closing",
                Name = "Interview Closing",
                Description = "Thank the candidate, provide next-step information, and finalize the session.",
                RequiredAgentIds = ["moderator"],
                CompletionKey = "Goal_ClosingComplete",
                EstimatedDurationMinutes = 5
            }
        };
    }
}
