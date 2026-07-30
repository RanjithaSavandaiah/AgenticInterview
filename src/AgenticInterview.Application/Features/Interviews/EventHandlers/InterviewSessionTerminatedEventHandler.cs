using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using AgenticInterview.Domain.Events;

namespace AgenticInterview.Application.Features.Interviews.EventHandlers;

public class InterviewSessionTerminatedEventHandler : INotificationHandler<InterviewSessionTerminatedEvent>
{
    private readonly ILogger<InterviewSessionTerminatedEventHandler> _logger;

    public InterviewSessionTerminatedEventHandler(ILogger<InterviewSessionTerminatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(InterviewSessionTerminatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Interview session {SessionId} was terminated. Reason: {Reason}", 
            notification.SessionId, notification.Reason);
            
        // Additional side-effects could go here (e.g., email to HR)
        return Task.CompletedTask;
    }
}
