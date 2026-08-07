using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Application.Abstractions;

namespace AgenticInterview.Infrastructure.RetrievalAugmentedGeneration;

/// <summary>
/// A lightweight, in-memory RAG service utilizing TF-IDF/Keyword scoring
/// to avoid dependency on paid Embedding models, fitting the "free things" requirement.
/// </summary>
public class ResumeRagService : IResumeRagService
{
    // candidateId -> list of document chunks
    private readonly ConcurrentDictionary<string, List<string>> _candidateChunks = new();

    public Task IngestResumeAsync(string candidateId, string resumeText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resumeText))
            return Task.CompletedTask;

        // Simple sentence/paragraph chunking
        var chunks = resumeText
            .Split(new[] { '\n', '\r', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Trim().Length > 20)
            .Select(x => x.Trim())
            .ToList();

        _candidateChunks.AddOrUpdate(candidateId, chunks, (_, _) => chunks);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> SearchCandidateExperienceAsync(string candidateId, string query, int topK = 3, CancellationToken cancellationToken = default)
    {
        if (!_candidateChunks.TryGetValue(candidateId, out var chunks))
            return Task.FromResult(Enumerable.Empty<string>());

        var queryTerms = query.ToLowerInvariant()
            .Split(new[] { ' ', ',', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 3)
            .ToList();

        if (queryTerms.Count == 0)
            return Task.FromResult(chunks.Take(topK));

        // Basic relevance scoring based on term frequency
        var scoredChunks = chunks
            .Select(chunk => 
            {
                var lowerChunk = chunk.ToLowerInvariant();
                int score = queryTerms.Sum(term => lowerChunk.Contains(term) ? 1 : 0);
                return new { Chunk = chunk, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk);

        return Task.FromResult(scoredChunks);
    }

    public Task<IEnumerable<(string CandidateId, string Text, double Score)>> SearchAllAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var queryTerms = query.ToLowerInvariant()
            .Split(new[] { ' ', ',', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 3)
            .ToList();

        if (queryTerms.Count == 0)
        {
            // No meaningful query terms — return first chunks from any candidate
            var fallback = _candidateChunks
                .SelectMany(kvp => kvp.Value.Take(topK).Select(chunk => (kvp.Key, chunk, 0.0)));
            return Task.FromResult(fallback.Take(topK));
        }

        // Score chunks across ALL candidates
        var results = _candidateChunks
            .SelectMany(kvp => kvp.Value.Select(chunk =>
            {
                var lowerChunk = chunk.ToLowerInvariant();
                double score = queryTerms.Sum(term => lowerChunk.Contains(term) ? 1.0 : 0.0) / queryTerms.Count;
                return (CandidateId: kvp.Key, Text: chunk, Score: score);
            }))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK);

        return Task.FromResult(results);
    }
}
