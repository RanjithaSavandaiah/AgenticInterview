using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Interfaces;

namespace AgenticInterview.Application.Features.Interviews.Commands;

public record ReportProctoringIncidentCommand(Guid SessionId, ProctoringViolationType Type, string AgentReasoning, bool IsConsideredStrike) : IRequest;

public class ReportProctoringIncidentCommandHandler : IRequestHandler<ReportProctoringIncidentCommand>
{
    private readonly IRepository<InterviewSession> _interviewRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReportProctoringIncidentCommandHandler(IRepository<InterviewSession> interviewRepository, IUnitOfWork unitOfWork)
    {
        _interviewRepository = interviewRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ReportProctoringIncidentCommand request, CancellationToken cancellationToken)
    {
        var session = await _interviewRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null) throw new ArgumentException("Session not found", nameof(request.SessionId));

        var incident = new ProctoringIncident(request.Type, request.AgentReasoning, request.IsConsideredStrike);
        session.RecordProctoringIncident(incident);

        await _interviewRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
