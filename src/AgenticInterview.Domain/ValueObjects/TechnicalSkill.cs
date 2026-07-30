namespace AgenticInterview.Domain.ValueObjects;

/// <summary>
/// Represents a technical skill and the candidate's assessed proficiency.
/// </summary>
public record TechnicalSkill(
    string Name,
    string AssessedProficiencyLevel,
    int Score
);
