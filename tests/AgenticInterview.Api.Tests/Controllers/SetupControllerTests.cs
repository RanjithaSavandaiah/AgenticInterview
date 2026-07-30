using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Api.Controllers;
using AgenticInterview.Application.Features.Setup.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgenticInterview.Api.Tests.Controllers;

public class SetupControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly SetupController _sut;

    public SetupControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<SetupController>>();
        _sut = new SetupController(_mediatorMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task UploadAndStart_MissingResume_ReturnsBadRequest()
    {
        // Act
        var result = await _sut.UploadAndStart(null!, CreateMockFile("job.pdf"));

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadAndStart_ValidFiles_ReturnsOkWithSessionId()
    {
        // Arrange
        var resume = CreateMockFile("resume.pdf");
        var job = CreateMockFile("job.pdf");
        var expectedSessionId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(It.IsAny<UploadAndStartCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSessionId);

        // Act
        var result = await _sut.UploadAndStart(resume, job) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        
        // Dynamic property access in tests using reflection
        var sessionIdProp = result.Value?.GetType().GetProperty("SessionId")?.GetValue(result.Value, null);
        Assert.Equal(expectedSessionId, sessionIdProp);
    }

    private IFormFile CreateMockFile(string fileName)
    {
        var fileMock = new Mock<IFormFile>();
        var content = "Hello World from a Fake File";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;

        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        
        return fileMock.Object;
    }
}
