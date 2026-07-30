using System;

namespace AgenticInterview.Domain.Entities;

public record InterviewRecordingMetadata(
    string FilePath,
    TimeSpan Duration,
    long SizeBytes
);
