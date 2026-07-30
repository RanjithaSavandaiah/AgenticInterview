using Microsoft.Extensions.Logging;

namespace AgenticInterview.Infrastructure.RecordingStorage;


/// <summary>
/// Local filesystem implementation of <see cref="IRecordingStorageService"/>.
/// Stores recordings under a "Recordings/{SessionId}/" directory.
/// </summary>
public class LocalRecordingStorageService : IRecordingStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalRecordingStorageService> _logger;

    public LocalRecordingStorageService(ILogger<LocalRecordingStorageService> logger, string basePath = "Recordings")
    {
        _basePath = basePath;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> SaveRecordingAsync(Guid sessionId, string fileName, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var sessionDir = Path.Combine(_basePath, sessionId.ToString());
        Directory.CreateDirectory(sessionDir);

        var filePath = Path.Combine(sessionDir, fileName);

        await using var output = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(output, cancellationToken);

        _logger.LogInformation("Recording saved: {FilePath}", filePath);
        return filePath;
    }

    /// <inheritdoc />
    public Task<Stream?> GetRecordingAsync(Guid sessionId, string fileName, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, sessionId.ToString(), fileName);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Recording not found: {FilePath}", filePath);
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListRecordingsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var sessionDir = Path.Combine(_basePath, sessionId.ToString());

        if (!Directory.Exists(sessionDir))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var files = Directory.GetFiles(sessionDir).Select(Path.GetFileName).Where(f => f != null).ToList();
        return Task.FromResult<IReadOnlyList<string>>(files!);
    }
}
