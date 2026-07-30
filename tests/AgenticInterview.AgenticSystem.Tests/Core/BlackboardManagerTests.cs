using System;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticInterview.AgenticSystem.Tests.Core;

public class BlackboardManagerTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<BlackboardManager>> _loggerMock;
    private readonly BlackboardManager _sut;

    public BlackboardManagerTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<BlackboardManager>>();
        
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);

        _sut = new BlackboardManager(_serviceProviderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void GetOrCreate_ReturnsSameInstanceForSameId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var first = _sut.GetOrCreate(sessionId);
        var second = _sut.GetOrCreate(sessionId);

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrCreate_ReturnsDifferentInstancesForDifferentIds()
    {
        // Arrange
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();

        // Act
        var first = _sut.GetOrCreate(sessionId1);
        var second = _sut.GetOrCreate(sessionId2);

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Remove_RemovesInstance()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var first = _sut.GetOrCreate(sessionId);

        // Act
        _sut.Remove(sessionId);
        var second = _sut.GetOrCreate(sessionId);

        // Assert
        Assert.NotSame(first, second);
    }
}
