namespace AgenticInterview.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a candidate's confidence metrics at a specific point in time.
/// </summary>
public record CandidateConfidenceMetrics(
    int WordsPerMinute,
    int PauseCount,
    int FillerWordCount,
    int EyeContactPercentage,
    string DominantFacialExpression
);
