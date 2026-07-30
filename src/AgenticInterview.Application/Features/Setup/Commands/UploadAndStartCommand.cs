using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Application.Abstractions;
using AgenticInterview.Application.Features.Interviews.Commands;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.AI;

namespace AgenticInterview.Application.Features.Setup.Commands;

public record UploadAndStartCommand(
    string CandidateFileName,
    Stream CandidateFileStream,
    string JobFileName,
    Stream JobFileStream) : IRequest<Guid>;

public class UploadAndStartCommandHandler : IRequestHandler<UploadAndStartCommand, Guid>
{
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IRepository<CandidateProfile> _candidateRepository;
    private readonly IRepository<JobDescriptionProfile> _jobRepository;
    private readonly IMediator _mediator;
    private readonly IChatClient _chatClient;

    public UploadAndStartCommandHandler(
        IDocumentIntelligenceService documentIntelligenceService,
        IRepository<CandidateProfile> candidateRepository,
        IRepository<JobDescriptionProfile> jobRepository,
        IMediator mediator,
        IChatClient chatClient)
    {
        _documentIntelligenceService = documentIntelligenceService;
        _candidateRepository = candidateRepository;
        _jobRepository = jobRepository;
        _mediator = mediator;
        _chatClient = chatClient;
    }

    public async Task<Guid> Handle(UploadAndStartCommand request, CancellationToken cancellationToken)
    {
        // 1. Extract Candidate Resume Text
        var resumeText = await ExtractTextAsync(request.CandidateFileName, request.CandidateFileStream, cancellationToken);
        
        // Use LLM to extract candidate's real name from the resume text
        var namePrompt = $"Extract the candidate's full name from this resume text. Output ONLY the candidate's full name and absolutely nothing else. If you cannot find a name, output 'Unknown Candidate'. Resume text:\n{resumeText}";
        var nameResponse = await _chatClient.GetResponseAsync(namePrompt, cancellationToken: cancellationToken);
        var candidateName = nameResponse.Text?.Trim();
        if (string.IsNullOrWhiteSpace(candidateName) || candidateName.Length > 100) 
        {
            candidateName = Path.GetFileNameWithoutExtension(request.CandidateFileName);
        }
        
        var candidate = new CandidateProfile(candidateName, "unknown@example.com", resumeText);
        await _candidateRepository.AddAsync(candidate, cancellationToken);

        // 2. Extract Job Description Text
        var jdText = await ExtractTextAsync(request.JobFileName, request.JobFileStream, cancellationToken);
        var jobTitle = Path.GetFileNameWithoutExtension(request.JobFileName);
        
        var jobDescription = new JobDescriptionProfile(jobTitle, TargetJobRole.Custom, jdText);
        await _jobRepository.AddAsync(jobDescription, cancellationToken);

        // 3. Start the interview session
        var startCommand = new StartInterviewCommand(candidate.Id, jobDescription.Id);
        return await _mediator.Send(startCommand, cancellationToken);
    }

    private async Task<string> ExtractTextAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".pdf")
        {
            return await _documentIntelligenceService.ExtractTextFromPdfAsync(stream, cancellationToken);
        }
        else if (ext == ".docx" || ext == ".doc")
        {
            return await _documentIntelligenceService.ExtractTextFromDocxAsync(stream, cancellationToken);
        }
        else
        {
            // Fallback for txt files
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
