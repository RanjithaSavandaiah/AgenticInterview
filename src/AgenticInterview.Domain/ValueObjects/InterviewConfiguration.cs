using AgenticInterview.Domain.Enums;
using System.Collections.Generic;

namespace AgenticInterview.Domain.ValueObjects;

/// <summary>
/// Configuration details for a specific interview session.
/// </summary>
public record InterviewConfiguration(
    int DurationMinutes,
    List<InterviewQuestionType> AllowedQuestionTypes,
    QuestionDifficultyLevel StartingDifficulty,
    bool IsProctoringStrict,
    int StrikeLimit
);
