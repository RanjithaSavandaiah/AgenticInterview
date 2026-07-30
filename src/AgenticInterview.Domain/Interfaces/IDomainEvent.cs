using System;

using MediatR;

namespace AgenticInterview.Domain.Interfaces;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}
