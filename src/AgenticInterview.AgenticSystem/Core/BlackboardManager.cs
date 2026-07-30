using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.AgenticSystem.Core;

public class BlackboardManager : IBlackboardManager
{
    private readonly ConcurrentDictionary<Guid, InterviewBlackboard> _blackboards = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BlackboardManager> _logger;

    public BlackboardManager(IServiceProvider serviceProvider, ILogger<BlackboardManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public InterviewBlackboard GetOrCreate(Guid sessionId)
    {
        return _blackboards.GetOrAdd(sessionId, id => 
        {
            var blackboard = new InterviewBlackboard(id);
            blackboard.MessageAdded += Blackboard_MessageAdded;
            _logger.LogInformation("Created new InterviewBlackboard for Session {SessionId}", id);
            return blackboard;
        });
    }

    public void Remove(Guid sessionId)
    {
        if (_blackboards.TryRemove(sessionId, out var blackboard))
        {
            blackboard.MessageAdded -= Blackboard_MessageAdded;
            _logger.LogInformation("Removed InterviewBlackboard for Session {SessionId}", sessionId);
        }
    }

    private void Blackboard_MessageAdded(object? sender, BlackboardMessage message)
    {
        if (sender is not InterviewBlackboard blackboard) return;

        // Use a background task to notify SignalR to avoid blocking the caller
        _ = System.Threading.Tasks.Task.Run(async () => 
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var notifier = scope.ServiceProvider.GetService<ISessionNotifier>();
                if (notifier != null)
                {
                    await notifier.NotifyMessageAddedAsync(blackboard.SessionId, message);
                }
            }
            catch (Exception ex)
            
            {
                _logger.LogError(ex, "Failed to notify MessageAdded for Session {SessionId}", blackboard.SessionId);
            }
        });
    }
}
