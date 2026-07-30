using System;

namespace AgenticInterview.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a score between 0 and 100.
/// </summary>
public record EvaluationScore
{
    public int Value { get; init; }
    
    public EvaluationScore(int value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), "Score must be between 0 and 100.");
            
        Value = value;
    }
}
