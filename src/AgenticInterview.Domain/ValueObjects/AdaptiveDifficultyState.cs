using AgenticInterview.Domain.Enums;

namespace AgenticInterview.Domain.ValueObjects;

/// <summary>
/// Represents the current difficulty state and recent performance trend.
/// </summary>
public record AdaptiveDifficultyState(
    QuestionDifficultyLevel CurrentLevel,
    double RecentPerformanceAverage,
    int QuestionsAskedAtCurrentLevel
);
