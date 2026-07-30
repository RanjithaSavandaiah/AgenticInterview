using System;
using System.Collections.Generic;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Interfaces;
using AgenticInterview.Domain.ValueObjects;

namespace AgenticInterview.Domain.Entities;

/// <summary>
/// The aggregate root representing an entire interview session.
/// Manages the state machine and coordinates questions and answers.
/// </summary>
public class InterviewSession : BaseEntity, IAggregateRoot
{
    private readonly List<InterviewQuestion> _questions = new();
    private readonly List<ProctoringIncident> _incidents = new();

    public Guid CandidateProfileId { get; private set; }
    public Guid JobDescriptionId { get; private set; }
    public InterviewConfiguration Configuration { get; private set; }
    public InterviewSessionStatus Status { get; private set; }
    public AdaptiveDifficultyState DifficultyState { get; private set; }
    public InterviewRecordingMetadata? RecordingMetadata { get; private set; }
    public EvaluationScore? FinalScore { get; private set; }
    public string Recommendation { get; private set; } = string.Empty;
    
    public IReadOnlyCollection<InterviewQuestion> Questions => _questions.AsReadOnly();
    public IReadOnlyCollection<ProctoringIncident> Incidents => _incidents.AsReadOnly();

    private InterviewSession() 
    { 
        Configuration = null!;
        DifficultyState = null!;
    }

    public InterviewSession(Guid candidateProfileId, Guid jobDescriptionId, InterviewConfiguration configuration)
    {
        CandidateProfileId = candidateProfileId;
        JobDescriptionId = jobDescriptionId;
        Configuration = configuration;
        Status = InterviewSessionStatus.NotStarted;
        DifficultyState = new AdaptiveDifficultyState(configuration.StartingDifficulty, 0.0, 0);
    }

    public void StartSession()
    {
        if (Status != InterviewSessionStatus.NotStarted)
            throw new InvalidOperationException("Session can only be started from NotStarted state.");

        Status = InterviewSessionStatus.InProgress;
        // Raise InterviewSessionStartedEvent (would be done here)
    }

    public void AskQuestion(InterviewQuestion question)
    {
        if (Status != InterviewSessionStatus.InProgress)
            throw new InvalidOperationException("Questions can only be asked when session is in progress.");

        _questions.Add(question);
        // Raise InterviewQuestionAskedEvent
    }

    public void RecordProctoringIncident(ProctoringIncident incident)
    {
        _incidents.Add(incident);
        if (Configuration.IsProctoringStrict && _incidents.Count >= Configuration.StrikeLimit)
        {
            TerminateSession("Maximum proctoring strikes exceeded.");
        }
        // Raise ProctoringViolationDetectedEvent
    }

    public void TerminateSession(string reason)
    {
        Status = InterviewSessionStatus.Terminated;
        // Raise InterviewSessionTerminatedEvent with reason
    }

    public void CompleteSession(EvaluationScore finalScore, string recommendation)
    {
        Status = InterviewSessionStatus.Completed;
        FinalScore = finalScore;
        Recommendation = recommendation;
        // Raise InterviewSessionCompletedEvent
    }
}
