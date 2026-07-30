using System;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.ValueObjects;
using Xunit;

namespace AgenticInterview.Domain.Tests.Entities;

public class InterviewSessionTests
{
    private readonly InterviewConfiguration _defaultConfig;

    public InterviewSessionTests()
    {
        _defaultConfig = new InterviewConfiguration(60, new System.Collections.Generic.List<InterviewQuestionType> { InterviewQuestionType.Theory }, QuestionDifficultyLevel.Medium, true, 3);
    }

    [Fact]
    public void StartSession_WhenNotStarted_ChangesStatusToInProgress()
    {
        // Arrange
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), _defaultConfig);

        // Act
        session.StartSession();

        // Assert
        Assert.Equal(InterviewSessionStatus.InProgress, session.Status);
    }

    [Fact]
    public void StartSession_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), _defaultConfig);
        session.StartSession();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => session.StartSession());
    }

    [Fact]
    public void RecordProctoringIncident_BelowStrikeLimit_AddsIncidentButDoesNotTerminate()
    {
        // Arrange
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), _defaultConfig);
        session.StartSession();

        // Act
        session.RecordProctoringIncident(new ProctoringIncident(ProctoringViolationType.TabSwitch, "test", true));

        // Assert
        Assert.Single(session.Incidents);
        Assert.Equal(InterviewSessionStatus.InProgress, session.Status);
    }

    [Fact]
    public void RecordProctoringIncident_ExceedsStrikeLimit_TerminatesSession()
    {
        // Arrange
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), _defaultConfig); // StrikeLimit is 3
        session.StartSession();

        // Act
        session.RecordProctoringIncident(new ProctoringIncident(ProctoringViolationType.TabSwitch, "1", true));
        session.RecordProctoringIncident(new ProctoringIncident(ProctoringViolationType.TabSwitch, "2", true));
        session.RecordProctoringIncident(new ProctoringIncident(ProctoringViolationType.TabSwitch, "3", true)); // 3rd strike

        // Assert
        Assert.Equal(3, session.Incidents.Count);
        Assert.Equal(InterviewSessionStatus.Terminated, session.Status);
    }

    [Fact]
    public void CompleteSession_SetsFinalScoreAndRecommendation()
    {
        // Arrange
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), _defaultConfig);
        session.StartSession();
        var score = new EvaluationScore(85);
        var recommendation = "Hire";

        // Act
        session.CompleteSession(score, recommendation);

        // Assert
        Assert.Equal(InterviewSessionStatus.Completed, session.Status);
        Assert.Equal(score, session.FinalScore);
        Assert.Equal(recommendation, session.Recommendation);
    }
}
