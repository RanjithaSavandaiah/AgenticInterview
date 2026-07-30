using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Interfaces;
using AgenticInterview.Application.Queries;
using AgenticInterview.Application.DataTransferObjects;

namespace AgenticInterview.Application.Features.Interviews.Queries;

public class GetInterviewSessionStatusQueryHandler : IRequestHandler<GetInterviewSessionStatusQuery, InterviewStatusDto?>
{
    private readonly IRepository<InterviewSession> _sessionRepository;
    private readonly IRepository<CandidateProfile> _candidateRepository;

    public GetInterviewSessionStatusQueryHandler(
        IRepository<InterviewSession> sessionRepository,
        IRepository<CandidateProfile> candidateRepository)
    {
        _sessionRepository = sessionRepository;
        _candidateRepository = candidateRepository;
    }

    public async Task<InterviewStatusDto?> Handle(GetInterviewSessionStatusQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null) return null;

        var candidate = await _candidateRepository.GetByIdAsync(session.CandidateProfileId, cancellationToken);
        var candidateName = candidate?.Name ?? "Unknown Candidate";

        return new InterviewStatusDto(
            session.Id,
            candidateName,
            "Software Engineer",
            session.Status,
            session.FinalScore?.Value ?? 0,
            session.Incidents.Count,
            session.Questions.Count,
            DateTime.UtcNow,
            null
        );
    }
}
