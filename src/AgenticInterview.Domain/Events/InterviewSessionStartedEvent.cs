using System;
using AgenticInterview.Domain.Interfaces;

namespace AgenticInterview.Domain.Events;

public class InterviewSessionStartedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public Guid SessionId { get; }
    public DateTimeOffset OccurredOn { get; }

    public InterviewSessionStartedEvent(Guid sessionId)
    {
        SessionId = sessionId;
        OccurredOn = DateTimeOffset.UtcNow;
    }
}
