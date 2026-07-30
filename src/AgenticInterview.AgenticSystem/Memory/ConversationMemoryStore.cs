using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticInterview.AgenticSystem.Memory;


/// <summary>
/// A semantic memory store for the agents to recall previous conversational context
/// overcoming standard LLM window limitations.
/// </summary>
public class ConversationMemoryStore : IConversationMemoryStore
{
    // sessionId -> list of memories
    private readonly ConcurrentDictionary<string, List<AgentMemory>> _sessionMemories = new();

    public Task SaveMemoryAsync(string sessionId, string memoryKey, string memoryText)
    {
        var memoryList = _sessionMemories.GetOrAdd(sessionId, _ => new List<AgentMemory>());
        memoryList.Add(new AgentMemory { Key = memoryKey, Text = memoryText, Timestamp = System.DateTime.UtcNow });
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> RetrieveRelevantMemoriesAsync(string sessionId, string contextQuery)
    {
        if (!_sessionMemories.TryGetValue(sessionId, out var memories))
            return Task.FromResult(Enumerable.Empty<string>());

        var queryTerms = contextQuery.ToLowerInvariant().Split(new[] { ' ', '?' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        // Retrieve top matches using basic keyword overlap (simulating Semantic Search for prototype)
        var results = memories
            .Select(m => new 
            {
                m.Text,
                Score = queryTerms.Count(qt => m.Text.ToLowerInvariant().Contains(qt))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .Select(x => x.Text);

        return Task.FromResult(results);
    }
}

