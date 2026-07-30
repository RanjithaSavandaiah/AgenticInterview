using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.AgenticSystem.Blackboard;

using System;
using System.Collections.Generic;
/// <summary>
/// Concrete implementation of <see cref="IBlackboardController"/>.
/// Provides controlled, thread-safe access to the underlying <see cref="InterviewBlackboard"/>.
/// </summary>
public class BlackboardController : IBlackboardController
{
    private readonly InterviewBlackboard _blackboard;

    /// <summary>
    /// Initializes a new <see cref="BlackboardController"/> wrapping the given blackboard.
    /// </summary>
    public BlackboardController(InterviewBlackboard blackboard)
    {
        _blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
    }

    /// <inheritdoc />
    public void Write<T>(string key, T value) where T : notnull
    {
        _blackboard.Set(key, value);
    }

    /// <inheritdoc />
    public T? Read<T>(string key)
    {
        return _blackboard.Get<T>(key);
    }

    /// <inheritdoc />
    public void PostMessage(string sourceAgent, string content)
    {
        _blackboard.AddMessage(new BlackboardMessage(sourceAgent, content, DateTime.UtcNow));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<BlackboardMessage> GetMessages()
    {
        return _blackboard.GetMessages();
    }

    /// <inheritdoc />
    public InterviewBlackboard GetBlackboard()
    {
        return _blackboard;
    }
}
