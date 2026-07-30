using System.Collections.Generic;
using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.AgenticSystem.Blackboard;

/// <summary>
/// Defines the contract for a Blackboard Controller that manages
/// read/write access to the shared <see cref="InterviewBlackboard"/>.
/// Implements the Blackboard architectural pattern for multi-agent coordination.
/// </summary>
public interface IBlackboardController
{
    /// <summary>
    /// Writes a typed value to the blackboard under the specified key.
    /// </summary>
    void Write<T>(string key, T value) where T : notnull;

    /// <summary>
    /// Reads a typed value from the blackboard by key.
    /// </summary>
    T? Read<T>(string key);

    /// <summary>
    /// Posts a message to the blackboard message log.
    /// </summary>
    void PostMessage(string sourceAgent, string content);

    /// <summary>
    /// Returns all messages posted to the blackboard.
    /// </summary>
    IReadOnlyCollection<BlackboardMessage> GetMessages();

    /// <summary>
    /// Returns the underlying blackboard instance.
    /// </summary>
    InterviewBlackboard GetBlackboard();
}
