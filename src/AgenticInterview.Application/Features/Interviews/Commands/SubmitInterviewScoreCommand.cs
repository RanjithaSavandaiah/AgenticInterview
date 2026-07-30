using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Interfaces;
using AgenticInterview.Domain.ValueObjects;

namespace AgenticInterview.Application.Features.Interviews.Commands;

public record SubmitInterviewScoreCommand(Guid SessionId, int CompositeScore, string Recommendation) : IRequest;

public class SubmitInterviewScoreCommandHandler : IRequestHandler<SubmitInterviewScoreCommand>
{
    private readonly IRepository<InterviewSession> _interviewRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitInterviewScoreCommandHandler(IRepository<InterviewSession> interviewRepository, IUnitOfWork unitOfWork)
    {
        _interviewRepository = interviewRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SubmitInterviewScoreCommand request, CancellationToken cancellationToken)
    {
        var session = await _interviewRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null) throw new ArgumentException("Session not found", nameof(request.SessionId));

        var score = new EvaluationScore(request.CompositeScore);
        session.CompleteSession(score, request.Recommendation);
        
        await _interviewRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
