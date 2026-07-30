using System;
using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.AgenticSystem.Core;

public interface IBlackboardManager
{
    InterviewBlackboard GetOrCreate(Guid sessionId);
    void Remove(Guid sessionId);
}
