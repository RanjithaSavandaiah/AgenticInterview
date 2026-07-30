using System;
using AgenticInterview.Domain.Interfaces;

namespace AgenticInterview.Domain.Events;

public class InterviewSessionTerminatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public Guid SessionId { get; }
    public string Reason { get; }
    public DateTimeOffset OccurredOn { get; }

    public InterviewSessionTerminatedEvent(Guid sessionId, string reason)
    {
        SessionId = sessionId;
        Reason = reason;
        OccurredOn = DateTimeOffset.UtcNow;
    }
}
