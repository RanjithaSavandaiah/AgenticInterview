using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace AgenticInterview.Infrastructure.RecordingStorage;

/// <summary>
/// Service responsible for storing and retrieving interview session recordings.
/// Saves audio/video recording metadata and binary streams to the local filesystem.
/// In production, this would be swapped for Azure Blob Storage, S3, etc. via the Interface Segregation Principle.
/// </summary>
public interface IRecordingStorageService
{
    /// <summary>
    /// Saves a recording file for the given session.
    /// </summary>
    Task<string> SaveRecordingAsync(Guid sessionId, string fileName, Stream fileStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a recording file stream for the given session and file name.
    /// </summary>
    Task<Stream?> GetRecordingAsync(Guid sessionId, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all recording file names for a given session.
    /// </summary>
    Task<IReadOnlyList<string>> ListRecordingsAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
