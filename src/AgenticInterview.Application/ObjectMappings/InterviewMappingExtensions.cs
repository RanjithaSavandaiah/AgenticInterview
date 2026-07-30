using AgenticInterview.Application.DataTransferObjects;
using AgenticInterview.Domain.Entities;

namespace AgenticInterview.Application.ObjectMappings;

/// <summary>
/// Manual mapping extensions between Domain entities and Application DTOs.
/// Using extension methods instead of AutoMapper to keep it lightweight,
/// reduce dependencies, and maintain full compile-time safety per SOLID principles.
/// </summary>
public static class InterviewMappingExtensions
{
    /// <summary>
    /// Maps an <see cref="InterviewSession"/> entity to an <see cref="InterviewStatusDto"/>.
    /// </summary>
    public static InterviewStatusDto ToStatusDto(this InterviewSession session, string candidateName, string jobTitle)
    {
        return new InterviewStatusDto(
            SessionId: session.Id,
            CandidateName: candidateName,
            JobTitle: jobTitle,
            Status: session.Status,
            CurrentScore: 0, // Computed externally by the EvaluationAgent
            ProctoringStrikeCount: session.Incidents.Count,
            QuestionsAsked: session.Questions.Count,
            StartedAt: session.CreatedAtUtc.DateTime,
            EndedAt: null);
    }

    /// <summary>
    /// Maps a <see cref="CandidateProfile"/> entity to a <see cref="CandidateProfileDto"/>.
    /// </summary>
    public static CandidateProfileDto ToDto(this CandidateProfile candidate)
    {
        return new CandidateProfileDto(
            Id: candidate.Id,
            FullName: candidate.Name,
            Email: candidate.Email,
            Skills: candidate.Skills.Select(s => s.ToString()).ToList(),
            ResumeText: candidate.ResumeTextContent);
    }
}
