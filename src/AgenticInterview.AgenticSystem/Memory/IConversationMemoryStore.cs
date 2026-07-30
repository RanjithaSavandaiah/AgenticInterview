using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgenticInterview.AgenticSystem.Memory;

public interface IConversationMemoryStore
{
    Task SaveMemoryAsync(string sessionId, string memoryKey, string memoryText);
    Task<IEnumerable<string>> RetrieveRelevantMemoriesAsync(string sessionId, string contextQuery);
}
