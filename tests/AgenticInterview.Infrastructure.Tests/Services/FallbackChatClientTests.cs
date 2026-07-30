using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Infrastructure.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticInterview.Infrastructure.Tests.Services;

public class FallbackChatClientTests
{
    private readonly Mock<IChatClient> _primaryMock;
    private readonly Mock<IChatClient> _fallbackMock;
    private readonly Mock<ILogger<FallbackChatClient>> _loggerMock;
    private readonly FallbackChatClient _sut;

    public FallbackChatClientTests()
    {
        _primaryMock = new Mock<IChatClient>();
        _fallbackMock = new Mock<IChatClient>();
        _loggerMock = new Mock<ILogger<FallbackChatClient>>();

        _sut = new FallbackChatClient(_primaryMock.Object, _fallbackMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetResponseAsync_PrimarySucceeds_ReturnsPrimaryResponse()
    {
        // Arrange
        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var expectedResponse = new ChatResponse(new[] { new ChatMessage(ChatRole.Assistant, "Hi") });
        
        _primaryMock.Setup(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.GetResponseAsync(messages);

        // Assert
        Assert.Equal(expectedResponse, result);
        _primaryMock.Verify(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _fallbackMock.Verify(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetResponseAsync_PrimaryFailsWithNon429_FallsBackImmediately()
    {
        // Arrange
        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var fallbackResponse = new ChatResponse(new[] { new ChatMessage(ChatRole.Assistant, "Fallback Hi") });

        _primaryMock.Setup(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Some random unhandled exception"));
            
        _fallbackMock.Setup(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResponse);

        // Act
        var result = await _sut.GetResponseAsync(messages);

        // Assert
        Assert.Equal(fallbackResponse, result);
        _primaryMock.Verify(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _fallbackMock.Verify(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetResponseAsync_PrimaryFailsWith429_RetriesAndFallsBackOnThirdFailure()
    {
        // Arrange
        var messages = new[] { new ChatMessage(ChatRole.User, "Hello") };
        var fallbackResponse = new ChatResponse(new[] { new ChatMessage(ChatRole.Assistant, "Fallback Hi") });

        _primaryMock.Setup(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("429 Too Many Requests"));
            
        _fallbackMock.Setup(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResponse);

        // Act
        var result = await _sut.GetResponseAsync(messages);

        // Assert
        Assert.Equal(fallbackResponse, result);
        _primaryMock.Verify(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _fallbackMock.Verify(c => c.GetResponseAsync(messages, It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
