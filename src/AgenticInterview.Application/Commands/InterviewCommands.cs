using System;
using MediatR;
using AgenticInterview.Application.DataTransferObjects;

namespace AgenticInterview.Application.Commands;

/// <summary>
/// Command to submit a candidate's answer to the current interview question.
/// Handled by dispatching the answer to the agent pipeline via the Blackboard.
/// </summary>
public record SubmitCandidateAnswerCommand(Guid SessionId, string Answer) : IRequest<bool>;

/// <summary>
/// Command to submit a candidate's code for evaluation.
/// </summary>
public record SubmitCandidateCodeCommand(Guid SessionId, string Code, string Language) : IRequest<bool>;

/// <summary>
/// Command to generate the final interview report.
/// </summary>
public record GenerateInterviewReportCommand(Guid SessionId) : IRequest<byte[]>;

/// <summary>
/// Command to end/terminate an active interview session.
/// </summary>
public record EndInterviewCommand(Guid SessionId, string Reason) : IRequest<bool>;
