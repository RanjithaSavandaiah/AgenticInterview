using System;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.Guardrails;
using AgenticInterview.AgenticSystem.State;
using AgenticInterview.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticInterview.Api.Tests.Hubs;

public class HrDashboardHubTests
{
    private readonly Mock<IBlackboardManager> _blackboardManagerMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly HrDashboardHub _sut;

    public HrDashboardHubTests()
    {
        _blackboardManagerMock = new Mock<IBlackboardManager>();
        
        _clientProxyMock = new Mock<IClientProxy>();
        _clientsMock = new Mock<IHubCallerClients>();
        _clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);

        var guardrails = new AgentGuardrails(new Mock<ILogger<AgentGuardrails>>().Object);

        _sut = new HrDashboardHub(_blackboardManagerMock.Object, guardrails)
        {
            Clients = _clientsMock.Object
        };
    }

    [Fact]
    public async Task SendInterviewUpdate_WithMalpractice_IncrementsStrikes()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var blackboard = new InterviewBlackboard(sessionId);
        blackboard.Set("ProctoringStrikeCount", 1);
        _blackboardManagerMock.Setup(m => m.GetOrCreate(sessionId)).Returns(blackboard);

        // Act
        await _sut.SendInterviewUpdate(sessionId.ToString(), "[MALPRACTICE] TAB_SWITCH");

        // Assert
        Assert.Equal(2, blackboard.Get<int>("ProctoringStrikeCount"));
        Assert.Equal("TAB_SWITCH", blackboard.Get<string>("PendingMalpractice"));
    }

    [Fact]
    public async Task SendInterviewUpdate_WithThirdMalpractice_TerminatesSessionAndNotifiesClients()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var blackboard = new InterviewBlackboard(sessionId);
        blackboard.Set("ProctoringStrikeCount", 2); // Next one will be 3rd
        _blackboardManagerMock.Setup(m => m.GetOrCreate(sessionId)).Returns(blackboard);

        // Act
        await _sut.SendInterviewUpdate(sessionId.ToString(), "[MALPRACTICE] WINDOW_BLUR");

        // Assert
        Assert.Equal(3, blackboard.Get<int>("ProctoringStrikeCount"));
        Assert.Equal("Terminated", blackboard.Get<string>("SessionStatus"));
        
        // Verify SignalR SendAsync was called. SendAsync is an extension method over SendCoreAsync.
        _clientProxyMock.Verify(c => c.SendCoreAsync(
            "ReceiveUpdate", 
            It.Is<object[]>(args => args.Length == 2 && args[0].ToString() == sessionId.ToString() && (args[1].ToString() ?? string.Empty).Contains("StatusChanged")), 
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
