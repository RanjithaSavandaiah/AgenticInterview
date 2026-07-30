using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AgenticInterview.AgenticSystem.Common;

namespace AgenticInterview.AgenticSystem.State;

/// <summary>
/// The central shared state (Blackboard) for the multi-agent system.
/// Agents read from and write to this blackboard.
/// </summary>
public class InterviewBlackboard
{
    public Guid SessionId { get; }
    
    // Concurrent dictionaries for thread-safe state sharing
    private readonly ConcurrentDictionary<string, object> _state = new();
    
    // Message history log
    private readonly ConcurrentQueue<BlackboardMessage> _messages = new();
    
    // Event fired when a new message is added
    public event EventHandler<BlackboardMessage>? MessageAdded;
    
    public InterviewBlackboard(Guid sessionId)
    {
        SessionId = sessionId;
    }

    public void Set<T>(string key, T value) where T : notnull
    {
        _state[key] = value;
    }

    public T? Get<T>(string key)
    {
        if (_state.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public void AddMessage(BlackboardMessage message)
    {
        _messages.Enqueue(message);
        MessageAdded?.Invoke(this, message);
        
        // Prevent unbounded memory growth by limiting the queue size
        while (_messages.Count > AgenticConstants.MaxBlackboardMessages)
        {
            _messages.TryDequeue(out _);
        }
    }

    public IReadOnlyCollection<BlackboardMessage> GetMessages()
    {
        return _messages.ToArray();
    }
}
