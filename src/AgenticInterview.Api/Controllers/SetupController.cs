using System.IO;
using System.Threading.Tasks;
using AgenticInterview.Application.Features.Setup.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgenticInterview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SetupController> _logger;

    public SetupController(IMediator mediator, ILogger<SetupController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("upload-and-start")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)] // 50 MB
    public async Task<IActionResult> UploadAndStart(
        [FromForm(Name = "candidateResume")] IFormFile? candidateResume,
        [FromForm(Name = "jobDescription")] IFormFile? jobDescription)
    {
        if (candidateResume == null || candidateResume.Length == 0)
        {
            return BadRequest(new { Status = 400, Title = "Validation Error", Detail = "Candidate resume is required." });
        }

        if (jobDescription == null || jobDescription.Length == 0)
        {
            return BadRequest(new { Status = 400, Title = "Validation Error", Detail = "Job description is required." });
        }

        _logger.LogInformation(
            "Received upload request: Resume={ResumeFile} ({ResumeSize} bytes), JD={JdFile} ({JdSize} bytes)",
            candidateResume.FileName, candidateResume.Length,
            jobDescription.FileName, jobDescription.Length);

        // Buffer file contents into memory streams BEFORE passing to the command handler.
        // This decouples the command from the HTTP request lifecycle, preventing
        // "Unexpected end of request content" errors if the request body stream
        // is disposed or the client disconnects after the controller returns.
        using var resumeStream = new MemoryStream();
        await candidateResume.CopyToAsync(resumeStream);
        resumeStream.Position = 0;

        using var jdStream = new MemoryStream();
        await jobDescription.CopyToAsync(jdStream);
        jdStream.Position = 0;

        var command = new UploadAndStartCommand(
            candidateResume.FileName,
            resumeStream,
            jobDescription.FileName,
            jdStream);

        var sessionId = await _mediator.Send(command);

        return Ok(new { SessionId = sessionId });
    }
}
