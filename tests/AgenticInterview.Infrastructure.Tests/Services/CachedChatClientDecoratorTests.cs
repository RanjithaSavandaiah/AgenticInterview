using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticInterview.Infrastructure.Tests.Services;

public class CachedChatClientDecoratorTests : IDisposable
{
    private readonly Mock<IChatClient> _innerClientMock;
    private readonly MemoryCache _memoryCache;
    private readonly Mock<ILogger<CachedChatClientDecorator>> _loggerMock;
    private readonly CachedChatClientDecorator _sut;

    public CachedChatClientDecoratorTests()
    {
        _innerClientMock = new Mock<IChatClient>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<CachedChatClientDecorator>>();

        _sut = new CachedChatClientDecorator(_innerClientMock.Object, _memoryCache, _loggerMock.Object);
    }

    [Fact]
    public async Task GetResponseAsync_CacheMiss_CallsInnerClientAndCaches()
    {
        // Arrange
        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var expectedResponse = new ChatResponse(new[] { new ChatMessage(ChatRole.Assistant, "Hi") });

        _innerClientMock.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result1 = await _sut.GetResponseAsync(messages);
        var result2 = await _sut.GetResponseAsync(messages);

        // Assert
        Assert.Equal(expectedResponse, result1);
        Assert.Equal(expectedResponse, result2);
        
        // Inner client should only be called once due to caching
        _innerClientMock.Verify(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _sut.Dispose();
    }
}
