using System;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.AgenticSystem.Core;

public interface ISessionNotifier
{
    Task NotifyMessageAddedAsync(Guid sessionId, BlackboardMessage message);
}
