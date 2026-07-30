namespace AgenticInterview.Domain.Enums;

/// <summary>
/// Represents the current state of an interview session.
/// </summary>
public enum InterviewSessionStatus
{
    NotStarted,
    InProgress,
    Paused,
    Completed,
    Terminated
}
