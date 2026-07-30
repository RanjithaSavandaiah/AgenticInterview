using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Application.Abstractions;
using AgenticInterview.Application.Features.Interviews.Commands;
using AgenticInterview.Application.Features.Setup.Commands;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace AgenticInterview.Application.Tests.Features.Setup;

public class UploadAndStartCommandHandlerTests
{
    private readonly Mock<IDocumentIntelligenceService> _docServiceMock;
    private readonly Mock<IRepository<CandidateProfile>> _candidateRepoMock;
    private readonly Mock<IRepository<JobDescriptionProfile>> _jobRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IChatClient> _chatClientMock;
    private readonly UploadAndStartCommandHandler _sut;

    public UploadAndStartCommandHandlerTests()
    {
        _docServiceMock = new Mock<IDocumentIntelligenceService>();
        _candidateRepoMock = new Mock<IRepository<CandidateProfile>>();
        _jobRepoMock = new Mock<IRepository<JobDescriptionProfile>>();
        _mediatorMock = new Mock<IMediator>();
        _chatClientMock = new Mock<IChatClient>();

        _sut = new UploadAndStartCommandHandler(
            _docServiceMock.Object,
            _candidateRepoMock.Object,
            _jobRepoMock.Object,
            _mediatorMock.Object,
            _chatClientMock.Object);
    }

    [Fact]
    public async Task Handle_ExtractsTextSavesProfilesAndStartsInterview()
    {
        // Arrange
        var resumeStream = new MemoryStream();
        var jdStream = new MemoryStream();
        var command = new UploadAndStartCommand("resume.pdf", resumeStream, "job.pdf", jdStream);

        _docServiceMock.Setup(d => d.ExtractTextFromPdfAsync(resumeStream, It.IsAny<CancellationToken>()))
            .ReturnsAsync("John Doe's Resume Text");
        _docServiceMock.Setup(d => d.ExtractTextFromPdfAsync(jdStream, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Software Engineer Job Description");

        var chatResponse = new ChatResponse(new[] { new ChatMessage(ChatRole.Assistant, "John Doe") });
        _chatClientMock.Setup(c => c.GetResponseAsync(It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var expectedSessionId = Guid.NewGuid();
        _mediatorMock.Setup(m => m.Send(It.IsAny<StartInterviewCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSessionId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(expectedSessionId, result);

        _candidateRepoMock.Verify(r => r.AddAsync(It.Is<CandidateProfile>(c => c.Name == "John Doe" && c.ResumeTextContent == "John Doe's Resume Text"), It.IsAny<CancellationToken>()), Times.Once);
        _jobRepoMock.Verify(r => r.AddAsync(It.Is<JobDescriptionProfile>(j => j.Title == "job" && j.DescriptionTextContent == "Software Engineer Job Description"), It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.IsAny<StartInterviewCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
