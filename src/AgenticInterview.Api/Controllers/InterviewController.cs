using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using AgenticInterview.Api.ActionFilters;
using AgenticInterview.Application.Features.Interviews.Commands;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Application.Features.Interviews.Queries;

namespace AgenticInterview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterviewController : ControllerBase
{
    private readonly IMediator _mediator;

    public InterviewController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Starts a new interview session. Idempotent — duplicate requests with the same
    /// idempotency key return the original session ID without creating a duplicate.
    /// </summary>
    [HttpPost("start")]
    [Idempotent(CacheDurationSeconds = 300)]
    public async Task<IActionResult> StartInterview([FromBody] StartInterviewRequest request)
    {
        var command = new StartInterviewCommand(request.CandidateId, request.JobDescriptionId);
        var sessionId = await _mediator.Send(command);
        return Ok(new { SessionId = sessionId });
    }

    [HttpGet("{sessionId}/status")]
    public async Task<IActionResult> GetStatus(Guid sessionId)
    {
        var query = new AgenticInterview.Application.Queries.GetInterviewSessionStatusQuery(sessionId);
        var statusDto = await _mediator.Send(query);
        if (statusDto == null) return NotFound();
        return Ok(statusDto);
    }

    [HttpGet("{sessionId}/messages")]
    public IActionResult GetMessages(Guid sessionId, [FromServices] AgenticInterview.AgenticSystem.Core.IBlackboardManager blackboardManager)
    {
        var blackboard = blackboardManager.GetOrCreate(sessionId);
        var messages = blackboard.GetMessages();
        var dtos = System.Linq.Enumerable.Select(messages, m => new {
            sourceAgent = m.SourceAgent,
            content = m.Content,
            timestamp = m.Timestamp.ToString("O")
        });
        return Ok(dtos);
    }

    /// <summary>
    /// Manually submits an evaluation score for a session.
    /// </summary>
    [HttpPost("{sessionId}/submit-score")]
    [Idempotent(CacheDurationSeconds = 600)]
    public async Task<IActionResult> SubmitScore(Guid sessionId, [FromBody] SubmitScoreRequest request)
    {
        var command = new SubmitInterviewScoreCommand(sessionId, request.CompositeScore, request.Recommendation);
        await _mediator.Send(command);
        return Ok(new { Status = "Score Submitted" });
    }

    /// <summary>
    /// Reports a proctoring incident. Idempotent — prevents double-counting strikes
    /// which could unfairly terminate a candidate's session.
    /// </summary>
    [HttpPost("{sessionId}/proctoring-incident")]
    [Idempotent(CacheDurationSeconds = 60)]
    public async Task<IActionResult> ReportProctoringIncident(Guid sessionId, [FromBody] ProctoringIncidentRequest request)
    {
        var violationType = Enum.Parse<ProctoringViolationType>(request.ViolationType, ignoreCase: true);
        var command = new ReportProctoringIncidentCommand(sessionId, violationType, request.AgentReasoning ?? "Detected by browser proctoring", request.IsStrike);
        await _mediator.Send(command);
        return Ok(new { Status = "Recorded", SessionId = sessionId, ViolationType = request.ViolationType });
    }

    [HttpGet("{sessionId}/report")]
    public async Task<IActionResult> GetReport(Guid sessionId, [FromServices] AgenticInterview.Application.Abstractions.IReportGenerator reportGenerator)
    {
        var pdfBytes = await reportGenerator.GenerateInterviewReportAsync(sessionId);
        return File(pdfBytes, "application/pdf", $"InterviewReport_{sessionId}.pdf");
    }
}

public record StartInterviewRequest(Guid CandidateId, Guid JobDescriptionId);
public record ProctoringIncidentRequest(string ViolationType, string? AgentReasoning = null, bool IsStrike = true);
public record SubmitScoreRequest(int CompositeScore, string Recommendation);
