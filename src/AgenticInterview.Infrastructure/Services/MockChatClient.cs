using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace AgenticInterview.Infrastructure.Services;

public class MockChatClient : IChatClient
{
    private readonly string _response;

    public MockChatClient(string response)
    {
        _response = response;
    }

    public void Dispose() { }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _response)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield break;
    }

    public object? GetService(System.Type serviceType, object? serviceKey = null) => null;
}
