using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Agents;
using AgenticInterview.AgenticSystem.AgentCards;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticInterview.AgenticSystem.Tests.Core;

public class SubAgentDelegatorTests
{
    private readonly AgentCardRegistry _registry;
    private readonly Mock<ILogger<SubAgentDelegator>> _loggerMock;

    public SubAgentDelegatorTests()
    {
        _registry = AgentCardRegistry.CreateDefault();
        _loggerMock = new Mock<ILogger<SubAgentDelegator>>();
    }

    /// <summary>
    /// Creates a ServiceProvider that returns the given agents from GetServices&lt;IAgent&gt;().
    /// </summary>
    private IServiceProvider CreateServiceProviderWithAgents(params IAgent[] agents)
    {
        var services = new ServiceCollection();
        foreach (var agent in agents)
        {
            services.AddSingleton<IAgent>(agent);
        }
        return services.BuildServiceProvider();
    }

    // -----------------------------------------------------------------------
    // 1. Happy path: parent delegates to child, receives result
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DelegateAsync_HappyPath_ReturnsSuccessfulResult()
    {
        // Arrange
        var codeExecutionMock = new Mock<IAgent>();
        codeExecutionMock.SetupGet(a => a.Name).Returns(AgenticConstants.CodeExecutionAgentName);
        codeExecutionMock
            .Setup(a => a.ExecuteAsync(It.IsAny<InterviewBlackboard>(), It.IsAny<CancellationToken>()))
            .Callback<InterviewBlackboard, CancellationToken>((bb, _) =>
            {
                // Simulate the sub-agent posting output to the blackboard
                bb.AddMessage(new BlackboardMessage(
                    AgenticConstants.CodeExecutionAgentName,
                    "Code looks good. Minor: consider using `using` statements.",
                    DateTime.UtcNow));
            })
            .Returns(Task.CompletedTask);

        var sp = CreateServiceProviderWithAgents(codeExecutionMock.Object);
        var delegator = new SubAgentDelegator(sp, _registry, _loggerMock.Object);
        var blackboard = new InterviewBlackboard(Guid.NewGuid());

        // Act
        var result = await delegator.DelegateAsync(
            parentAgentName: AgenticConstants.TechnicalInterviewerName,
            targetAgentName: AgenticConstants.CodeExecutionAgentName,
            taskPrompt: "Review the candidate's code for correctness.",
            blackboard: blackboard);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(AgenticConstants.CodeExecutionAgentName, result.AgentName);
        Assert.Contains("Code looks good", result.Output);
        Assert.True(result.DurationMs >= 0);
        Assert.Null(result.ErrorMessage);
    }

    // -----------------------------------------------------------------------
    // 2. Depth guard: delegation at max depth is rejected
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DelegateAsync_AtMaxDepth_ReturnsFailedResult()
    {
        // Arrange
        var agentMock = new Mock<IAgent>();
        agentMock.SetupGet(a => a.Name).Returns(AgenticConstants.CodeExecutionAgentName);

        var sp = CreateServiceProviderWithAgents(agentMock.Object);
        var delegator = new SubAgentDelegator(sp, _registry, _loggerMock.Object);
        var blackboard = new InterviewBlackboard(Guid.NewGuid());

        // Create a context that is already at max depth
        var parentContext = new SubAgentContext
        {
            ParentAgentName = AgenticConstants.TechnicalInterviewerName,
            CurrentDepth = AgenticConstants.MaxSubAgentDepth, // Already at max
            MaxDepth = AgenticConstants.MaxSubAgentDepth,
            TaskDescription = "Original task"
        };

        // Act
        var result = await delegator.DelegateAsync(
            parentAgentName: AgenticConstants.CodeExecutionAgentName,
            targetAgentName: "Some Agent",
            taskPrompt: "This should be rejected.",
            blackboard: blackboard,
            parentContext: parentContext);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("maximum nesting depth", result.ErrorMessage);

        // The target agent should NOT have been executed
        agentMock.Verify(a => a.ExecuteAsync(It.IsAny<InterviewBlackboard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // 3. Timeout: sub-agent exceeding timeout is cancelled
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DelegateAsync_SubAgentTimesOut_ReturnsTimeoutResult()
    {
        // Arrange — create a sub-agent that throws OperationCanceledException
        // to simulate a timeout (the internal timeout CTS was cancelled, not the parent)
        var slowAgentMock = new Mock<IAgent>();
        slowAgentMock.SetupGet(a => a.Name).Returns(AgenticConstants.CodeExecutionAgentName);
        slowAgentMock
            .Setup(a => a.ExecuteAsync(It.IsAny<InterviewBlackboard>(), It.IsAny<CancellationToken>()))
            .Returns<InterviewBlackboard, CancellationToken>(async (_, ct) =>
            {
                // Simulate the sub-agent running until its timeout CTS fires
                // In production, the delegator creates a linked CTS with CancelAfter().
                // When it fires, ct.IsCancellationRequested becomes true and Task.Delay throws.
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            });

        var sp = CreateServiceProviderWithAgents(slowAgentMock.Object);
        var delegator = new SubAgentDelegator(sp, _registry, _loggerMock.Object);
        var blackboard = new InterviewBlackboard(Guid.NewGuid());

        // Act — use a parent CancellationToken that is NOT cancelled.
        // The delegator's internal timeout (SubAgentTimeoutSeconds) will fire instead.
        // But SubAgentTimeoutSeconds is 30s which is too long for a unit test,
        // so we test the behavior by verifying the delegator handles the exception correctly.
        // We pass a short-lived parent token and catch the re-thrown OperationCanceledException.
        // Instead, let's verify that a sub-agent throwing a generic exception is handled.
        // The real timeout test needs the internal CTS, which we can't control externally.
        // So let's simulate it: the agent throws TaskCanceledException explicitly.
        var explicitTimeoutAgentMock = new Mock<IAgent>();
        explicitTimeoutAgentMock.SetupGet(a => a.Name).Returns(AgenticConstants.CodeExecutionAgentName);
        explicitTimeoutAgentMock
            .Setup(a => a.ExecuteAsync(It.IsAny<InterviewBlackboard>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("The operation was canceled."));

        var sp2 = CreateServiceProviderWithAgents(explicitTimeoutAgentMock.Object);
        var delegator2 = new SubAgentDelegator(sp2, _registry, _loggerMock.Object);

        // The parent token is NOT cancelled, so the delegator interprets the
        // OperationCanceledException as a sub-agent timeout
        var result = await delegator2.DelegateAsync(
            parentAgentName: AgenticConstants.TechnicalInterviewerName,
            targetAgentName: AgenticConstants.CodeExecutionAgentName,
            taskPrompt: "This will time out.",
            blackboard: blackboard);

        // Assert — should be a timeout/error result
        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage);
    }

    // -----------------------------------------------------------------------
    // 4. Card guard: delegation to an unauthorized agent is rejected
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DelegateAsync_UnauthorizedDelegation_ReturnsFailedResult()
    {
        // Arrange — Technical Interviewer can delegate to code-execution and web-search,
        // but NOT to hr-observer
        var hrAgentMock = new Mock<IAgent>();
        hrAgentMock.SetupGet(a => a.Name).Returns(AgenticConstants.HrObserverAgentName);

        var sp = CreateServiceProviderWithAgents(hrAgentMock.Object);
        var delegator = new SubAgentDelegator(sp, _registry, _loggerMock.Object);
        var blackboard = new InterviewBlackboard(Guid.NewGuid());

        // Act
        var result = await delegator.DelegateAsync(
            parentAgentName: AgenticConstants.TechnicalInterviewerName,
            targetAgentName: AgenticConstants.HrObserverAgentName,
            taskPrompt: "This delegation is not allowed.",
            blackboard: blackboard);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not authorized", result.ErrorMessage);

        // The target agent should NOT have been executed
        hrAgentMock.Verify(a => a.ExecuteAsync(It.IsAny<InterviewBlackboard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // 5. Error handling: sub-agent throwing returns a failed SubAgentResult
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DelegateAsync_SubAgentThrows_ReturnsFailedResult()
    {
        // Arrange
        var failingAgentMock = new Mock<IAgent>();
        failingAgentMock.SetupGet(a => a.Name).Returns(AgenticConstants.CodeExecutionAgentName);
        failingAgentMock
            .Setup(a => a.ExecuteAsync(It.IsAny<InterviewBlackboard>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM API call failed"));

        var sp = CreateServiceProviderWithAgents(failingAgentMock.Object);
        var delegator = new SubAgentDelegator(sp, _registry, _loggerMock.Object);
        var blackboard = new InterviewBlackboard(Guid.NewGuid());

        // Act
        var result = await delegator.DelegateAsync(
            parentAgentName: AgenticConstants.TechnicalInterviewerName,
            targetAgentName: AgenticConstants.CodeExecutionAgentName,
            taskPrompt: "This will throw.",
            blackboard: blackboard);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("LLM API call failed", result.ErrorMessage);
        Assert.True(result.DurationMs >= 0);
    }

    // -----------------------------------------------------------------------
    // 6. Agent not found: delegation to a non-existent agent returns error
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DelegateAsync_AgentNotFound_ReturnsFailedResult()
    {
        // Arrange — empty service provider with no agents
        var sp = CreateServiceProviderWithAgents();

        // Use a registry where the parent has no card (so card guard is skipped)
        var emptyRegistry = new AgentCardRegistry();
        var delegator = new SubAgentDelegator(sp, emptyRegistry, _loggerMock.Object);
        var blackboard = new InterviewBlackboard(Guid.NewGuid());

        // Act
        var result = await delegator.DelegateAsync(
            parentAgentName: "Unknown Parent",
            targetAgentName: "Non-Existent Agent",
            taskPrompt: "This agent doesn't exist.",
            blackboard: blackboard);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }
}
