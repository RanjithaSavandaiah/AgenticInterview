using System;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Agents;
using AgenticInterview.AgenticSystem.AgentCards;
using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.State;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticInterview.AgenticSystem.Tests.Core;

public class InterviewOrchestratorTests
{
    private readonly Mock<IAgent> _agent1Mock;
    private readonly Mock<IAgent> _agent2Mock;
    private readonly AgentCardRegistry _registry;
    private readonly Mock<IChatClient> _chatClientMock;
    private readonly Mock<ILogger<InterviewOrchestrator>> _loggerMock;
    private readonly InterviewOrchestrator _sut;

    public InterviewOrchestratorTests()
    {
        _agent1Mock = new Mock<IAgent>();
        _agent1Mock.SetupGet(a => a.Name).Returns("Agent 1");

        _agent2Mock = new Mock<IAgent>();
        _agent2Mock.SetupGet(a => a.Name).Returns("Agent 2");

        _registry = new AgentCardRegistry();
        _chatClientMock = new Mock<IChatClient>();
        _loggerMock = new Mock<ILogger<InterviewOrchestrator>>();

        _sut = new InterviewOrchestrator(
            new[] { _agent1Mock.Object, _agent2Mock.Object },
            _registry,
            _chatClientMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task RunCycleAsync_ExecutesAllAgents_WhenAllGoalsCompleted()
    {
        // Arrange — mark all goals completed so the orchestrator falls back to running all agents
        var blackboard = new InterviewBlackboard(Guid.NewGuid());
        blackboard.Set("Goal_IntroComplete", "true");
        blackboard.Set("Goal_TechnicalComplete", "true");
        blackboard.Set("Goal_BehavioralComplete", "true");
        blackboard.Set("Goal_EvaluationComplete", "true");
        blackboard.Set("Goal_ClosingComplete", "true");

        // Act
        await _sut.RunCycleAsync(blackboard);

        // Assert — with no active goal, the orchestrator runs all agents (fallback mode)
        _agent1Mock.Verify(a => a.ExecuteAsync(blackboard, It.IsAny<CancellationToken>()), Times.Once);
        _agent2Mock.Verify(a => a.ExecuteAsync(blackboard, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunFullInterviewAsync_RunsUntilTerminated()
    {
        // Arrange — mark all goals completed so agents are selected in fallback mode
        var blackboard = new InterviewBlackboard(Guid.NewGuid());
        blackboard.Set("Goal_IntroComplete", "true");
        blackboard.Set("Goal_TechnicalComplete", "true");
        blackboard.Set("Goal_BehavioralComplete", "true");
        blackboard.Set("Goal_EvaluationComplete", "true");
        blackboard.Set("Goal_ClosingComplete", "true");
        int runCount = 0;
        
        _agent1Mock.Setup(a => a.ExecuteAsync(blackboard, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                runCount++;
                if (runCount >= 2)
                {
                    blackboard.Set(AgenticConstants.SessionStatusKey, AgenticConstants.StatusTerminated);
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // increased safeguard for 1s cycle delays
        await _sut.RunFullInterviewAsync(blackboard, cts.Token);

        // Assert
        Assert.True(runCount >= 2, $"Expected at least 2 runs, got {runCount}");
    }

    [Fact]
    public async Task RunCycleAsync_GoalDriven_OnlyRunsMatchingAgents()
    {
        // Arrange — set up an agent whose name matches a goal's registered agent card
        var moderatorMock = new Mock<IAgent>();
        moderatorMock.SetupGet(a => a.Name).Returns(AgenticConstants.ModeratorAgentName);

        var techMock = new Mock<IAgent>();
        techMock.SetupGet(a => a.Name).Returns(AgenticConstants.TechnicalInterviewerName);

        // Register agent cards so goal lookup finds them
        var registry = AgentCardRegistry.CreateDefault();

        var orchestrator = new InterviewOrchestrator(
            new[] { moderatorMock.Object, techMock.Object },
            registry,
            _chatClientMock.Object,
            _loggerMock.Object);

        // Blackboard with no goals completed — first goal is "goal-intro" requiring moderator + behavioral
        var blackboard = new InterviewBlackboard(Guid.NewGuid());

        // Act
        await orchestrator.RunCycleAsync(blackboard);

        // Assert — moderator matches "goal-intro", tech interviewer does NOT (it's for "goal-technical")
        moderatorMock.Verify(a => a.ExecuteAsync(blackboard, It.IsAny<CancellationToken>()), Times.Once);
        techMock.Verify(a => a.ExecuteAsync(blackboard, It.IsAny<CancellationToken>()), Times.Never);
    }
}
