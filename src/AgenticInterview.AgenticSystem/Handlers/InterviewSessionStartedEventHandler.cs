using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using AgenticInterview.Domain.Events;
using AgenticInterview.AgenticSystem.Core;

namespace AgenticInterview.AgenticSystem.Handlers;

/// <summary>
/// Handles the <see cref="InterviewSessionStartedEvent"/> by enqueuing the session
/// into the <see cref="InterviewBackgroundService"/> for structured background processing.
/// 
/// This replaces the previous fire-and-forget <c>Task.Run</c> pattern with a lifecycle-managed
/// approach that supports graceful shutdown and per-session cancellation.
/// </summary>
public class InterviewSessionStartedEventHandler : INotificationHandler<InterviewSessionStartedEvent>
{
    private readonly InterviewBackgroundService _backgroundService;
    private readonly ILogger<InterviewSessionStartedEventHandler> _logger;

    public InterviewSessionStartedEventHandler(
        InterviewBackgroundService backgroundService,
        ILogger<InterviewSessionStartedEventHandler> logger)
    {
        _backgroundService = backgroundService;
        _logger = logger;
    }

    public async Task Handle(InterviewSessionStartedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received InterviewSessionStartedEvent for Session {SessionId}. Enqueuing for background orchestration.",
            notification.SessionId);

        await _backgroundService.EnqueueSessionAsync(notification.SessionId, cancellationToken);
    }
}
