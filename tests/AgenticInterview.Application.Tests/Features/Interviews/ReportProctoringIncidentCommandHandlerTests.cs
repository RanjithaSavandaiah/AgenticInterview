using System;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Application.Features.Interviews.Commands;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Interfaces;
using AgenticInterview.Domain.ValueObjects;
using Moq;
using Xunit;

namespace AgenticInterview.Application.Tests.Features.Interviews;

public class ReportProctoringIncidentCommandHandlerTests
{
    private readonly Mock<IRepository<InterviewSession>> _interviewRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly ReportProctoringIncidentCommandHandler _sut;

    public ReportProctoringIncidentCommandHandlerTests()
    {
        _interviewRepoMock = new Mock<IRepository<InterviewSession>>();
        _uowMock = new Mock<IUnitOfWork>();

        _sut = new ReportProctoringIncidentCommandHandler(_interviewRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsArgumentException()
    {
        // Arrange
        var command = new ReportProctoringIncidentCommand(Guid.NewGuid(), ProctoringViolationType.TabSwitch, "Left window", true);
        _interviewRepoMock.Setup(r => r.GetByIdAsync(command.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InterviewSession?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidRequest_RecordsIncidentAndUpdateRepo()
    {
        // Arrange
        var command = new ReportProctoringIncidentCommand(Guid.NewGuid(), ProctoringViolationType.CopyPaste, "Copied text", true);
        var config = new InterviewConfiguration(60, new System.Collections.Generic.List<InterviewQuestionType> { InterviewQuestionType.Theory }, QuestionDifficultyLevel.Easy, true, 3);
        var session = new InterviewSession(Guid.NewGuid(), Guid.NewGuid(), config);
        session.StartSession();

        _interviewRepoMock.Setup(r => r.GetByIdAsync(command.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.Single(session.Incidents);
        _interviewRepoMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
