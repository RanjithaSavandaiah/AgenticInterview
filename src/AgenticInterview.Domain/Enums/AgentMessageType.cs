namespace AgenticInterview.Domain.Enums;

/// <summary>
/// Represents the type of message sent by an agent.
/// </summary>
public enum AgentMessageType
{
    Question,
    FollowUp,
    Hint,
    Warning,
    Feedback,
    General
}
