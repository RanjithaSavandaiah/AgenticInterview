using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Api.Controllers;
using AgenticInterview.Application.Features.Interviews.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.Api.Tests.Controllers;

public class InterviewControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly InterviewController _sut;

    public InterviewControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _sut = new InterviewController(_mediatorMock.Object);
    }

    [Fact]
    public async Task StartInterview_ReturnsOkWithSessionId()
    {
        // Arrange
        var request = new StartInterviewRequest(Guid.NewGuid(), Guid.NewGuid());
        var expectedSessionId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(It.IsAny<StartInterviewCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSessionId);

        // Act
        var result = await _sut.StartInterview(request) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        
        var sessionIdProp = result.Value?.GetType().GetProperty("SessionId")?.GetValue(result.Value, null);
        Assert.Equal(expectedSessionId, sessionIdProp);
    }

    [Fact]
    public async Task SubmitScore_ReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new SubmitScoreRequest(85, "Strong Hire");

        _mediatorMock.Setup(m => m.Send(It.IsAny<SubmitInterviewScoreCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.SubmitScore(sessionId, request) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task ReportProctoringIncident_ParsesViolationTypeAndReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new ProctoringIncidentRequest("TabSwitch", "User switched tab", true);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ReportProctoringIncidentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ReportProctoringIncident(sessionId, request) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public void GetMessages_RetrievesFromBlackboard()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var blackboardManagerMock = new Mock<IBlackboardManager>();
        var blackboard = new InterviewBlackboard(sessionId);
        
        var message = new BlackboardMessage("TestAgent", "Hello", DateTime.UtcNow);
        blackboard.AddMessage(message);

        blackboardManagerMock.Setup(bm => bm.GetOrCreate(sessionId)).Returns(blackboard);

        // Act
        var result = _sut.GetMessages(sessionId, blackboardManagerMock.Object) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }
}
