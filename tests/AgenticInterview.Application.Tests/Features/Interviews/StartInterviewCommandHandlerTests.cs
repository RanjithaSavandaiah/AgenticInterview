using System;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Application.Features.Interviews.Commands;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Events;
using AgenticInterview.Domain.Interfaces;
using MediatR;
using Moq;
using Xunit;

namespace AgenticInterview.Application.Tests.Features.Interviews;

public class StartInterviewCommandHandlerTests
{
    private readonly Mock<IRepository<InterviewSession>> _interviewRepoMock;
    private readonly Mock<IRepository<CandidateProfile>> _candidateRepoMock;
    private readonly Mock<IRepository<JobDescriptionProfile>> _jobRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly StartInterviewCommandHandler _sut;

    public StartInterviewCommandHandlerTests()
    {
        _interviewRepoMock = new Mock<IRepository<InterviewSession>>();
        _candidateRepoMock = new Mock<IRepository<CandidateProfile>>();
        _jobRepoMock = new Mock<IRepository<JobDescriptionProfile>>();
        _uowMock = new Mock<IUnitOfWork>();
        _mediatorMock = new Mock<IMediator>();

        _sut = new StartInterviewCommandHandler(
            _interviewRepoMock.Object,
            _candidateRepoMock.Object,
            _jobRepoMock.Object,
            _uowMock.Object,
            _mediatorMock.Object);
    }

    [Fact]
    public async Task Handle_CandidateOrJobNotFound_ThrowsArgumentException()
    {
        // Arrange
        var command = new StartInterviewCommand(Guid.NewGuid(), Guid.NewGuid());
        _candidateRepoMock.Setup(r => r.GetByIdAsync(command.CandidateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CandidateProfile?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesSessionAndPublishesEvent()
    {
        // Arrange
        var command = new StartInterviewCommand(Guid.NewGuid(), Guid.NewGuid());
        
        var candidate = new CandidateProfile("John", "john@example.com", "Resume");
        var job = new JobDescriptionProfile("Dev", TargetJobRole.Backend, "JD");
        
        _candidateRepoMock.Setup(r => r.GetByIdAsync(command.CandidateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidate);
        _jobRepoMock.Setup(r => r.GetByIdAsync(command.JobDescriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _interviewRepoMock.Verify(r => r.AddAsync(It.Is<InterviewSession>(s => s.CandidateProfileId == command.CandidateId), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Publish(It.Is<InterviewSessionStartedEvent>(e => e.SessionId == result), It.IsAny<CancellationToken>()), Times.Once);
    }
}
