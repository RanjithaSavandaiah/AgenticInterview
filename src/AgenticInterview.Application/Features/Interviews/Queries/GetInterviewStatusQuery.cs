using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Interfaces;

namespace AgenticInterview.Application.Features.Interviews.Queries;

public record GetInterviewStatusQuery(Guid SessionId) : IRequest<InterviewSessionStatus>;

public class GetInterviewStatusQueryHandler : IRequestHandler<GetInterviewStatusQuery, InterviewSessionStatus>
{
    private readonly IRepository<InterviewSession> _interviewRepository;

    public GetInterviewStatusQueryHandler(IRepository<InterviewSession> interviewRepository)
    {
        _interviewRepository = interviewRepository;
    }

    public async Task<InterviewSessionStatus> Handle(GetInterviewStatusQuery request, CancellationToken cancellationToken)
    {
        var session = await _interviewRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null) throw new ArgumentException("Session not found", nameof(request.SessionId));

        return session.Status;
    }
}
