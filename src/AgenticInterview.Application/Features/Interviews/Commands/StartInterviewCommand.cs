using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Interfaces;
using AgenticInterview.Domain.ValueObjects;

namespace AgenticInterview.Application.Features.Interviews.Commands;

public record StartInterviewCommand(Guid CandidateId, Guid JobDescriptionId) : IRequest<Guid>;

public class StartInterviewCommandHandler : IRequestHandler<StartInterviewCommand, Guid>
{
    private readonly IRepository<InterviewSession> _interviewRepository;
    private readonly IRepository<CandidateProfile> _candidateRepository;
    private readonly IRepository<JobDescriptionProfile> _jobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public StartInterviewCommandHandler(
        IRepository<InterviewSession> interviewRepository,
        IRepository<CandidateProfile> candidateRepository,
        IRepository<JobDescriptionProfile> jobRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _interviewRepository = interviewRepository;
        _candidateRepository = candidateRepository;
        _jobRepository = jobRepository;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    public async Task<Guid> Handle(StartInterviewCommand request, CancellationToken cancellationToken)
    {
        var candidate = await _candidateRepository.GetByIdAsync(request.CandidateId, cancellationToken);
        var job = await _jobRepository.GetByIdAsync(request.JobDescriptionId, cancellationToken);

        if (candidate == null) throw new ArgumentException("Candidate not found", nameof(request.CandidateId));
        if (job == null) throw new ArgumentException("Job Description not found", nameof(request.JobDescriptionId));

        var config = new InterviewConfiguration(
            DurationMinutes: 60,
            AllowedQuestionTypes: new List<InterviewQuestionType> { InterviewQuestionType.Theory, InterviewQuestionType.Coding, InterviewQuestionType.Behavioral },
            StartingDifficulty: QuestionDifficultyLevel.Medium,
            IsProctoringStrict: true,
            StrikeLimit: 3
        );

        var session = new InterviewSession(request.CandidateId, request.JobDescriptionId, config);
        
        session.StartSession();

        await _interviewRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new AgenticInterview.Domain.Events.InterviewSessionStartedEvent(session.Id), cancellationToken);

        return session.Id;
    }
}
