using System;
using System.Collections.Generic;
using AgenticInterview.Domain.Enums;

namespace AgenticInterview.Application.DataTransferObjects;

/// <summary>
/// DTO representing the result of starting a new interview session.
/// </summary>
public record StartInterviewResponse(Guid SessionId, string Status, DateTime StartedAt);

/// <summary>
/// DTO for the interview status query response.
/// </summary>
public record InterviewStatusDto(
    Guid SessionId,
    string CandidateName,
    string JobTitle,
    InterviewSessionStatus Status,
    int CurrentScore,
    int ProctoringStrikeCount,
    int QuestionsAsked,
    DateTime StartedAt,
    DateTime? EndedAt);

/// <summary>
/// DTO for submitting a candidate's answer.
/// </summary>
public record SubmitAnswerRequest(Guid SessionId, string Answer);

/// <summary>
/// DTO for submitting candidate code.
/// </summary>
public record SubmitCodeRequest(Guid SessionId, string Code, string Language);

/// <summary>
/// DTO for reporting a proctoring incident.
/// </summary>
public record ProctoringIncidentRequest(Guid SessionId, string ViolationType, string? Details);

/// <summary>
/// DTO for the final interview report.
/// </summary>
public record InterviewReportDto(
    Guid SessionId,
    string CandidateName,
    string JobTitle,
    int FinalScore,
    string Recommendation,
    IReadOnlyList<TranscriptEntryDto> Transcript,
    IReadOnlyList<ProctoringEventDto> ProctoringEvents);

/// <summary>
/// DTO for a single transcript entry.
/// </summary>
public record TranscriptEntryDto(string Speaker, string Content, DateTime Timestamp);

/// <summary>
/// DTO for a proctoring event.
/// </summary>
public record ProctoringEventDto(string ViolationType, string Details, DateTime OccurredAt);

/// <summary>
/// DTO for the candidate profile summary.
/// </summary>
public record CandidateProfileDto(
    Guid Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Skills,
    string? ResumeText);
