using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Interfaces;
using Whisper.net;
using Whisper.net.Ggml;
using System.IO;

namespace AgenticInterview.Application.Features.Interviews.Commands;

public record ProcessCandidateAudioCommand(Guid SessionId, byte[] AudioData) : IRequest<string>;

public class ProcessCandidateAudioCommandHandler : IRequestHandler<ProcessCandidateAudioCommand, string>
{
    private readonly IRepository<InterviewSession> _interviewRepository;

    public ProcessCandidateAudioCommandHandler(IRepository<InterviewSession> interviewRepository)
    {
        _interviewRepository = interviewRepository;
    }

    public async Task<string> Handle(ProcessCandidateAudioCommand request, CancellationToken cancellationToken)
    {
        var session = await _interviewRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session == null) throw new ArgumentException("Session not found", nameof(request.SessionId));

        var modelName = "ggml-base.bin";
        if (!File.Exists(modelName))
        {
            using var client = new System.Net.Http.HttpClient();
            using var modelStream = await client.GetStreamAsync("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin");
            using var fileWriter = File.OpenWrite(modelName);
            await modelStream.CopyToAsync(fileWriter);
        }

        using var whisperFactory = WhisperFactory.FromPath(modelName);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage("en")
            .Build();

        // Whisper.net requires PCM 16-bit 16kHz audio stream.
        // For the sake of this prototype, we assume request.AudioData is correctly formatted WAV/PCM data.
        using var ms = new MemoryStream(request.AudioData);
        var transcript = "";
        
        await foreach (var result in processor.ProcessAsync(ms, cancellationToken))
        {
            transcript += result.Text + " ";
        }

        return string.IsNullOrWhiteSpace(transcript) ? "[No speech detected]" : transcript.Trim();
    }
}
