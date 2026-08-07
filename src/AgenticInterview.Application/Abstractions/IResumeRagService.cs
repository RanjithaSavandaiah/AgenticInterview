using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticInterview.Application.Abstractions;

public interface IResumeRagService
{
    Task IngestResumeAsync(string candidateId, string resumeText, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> SearchCandidateExperienceAsync(string candidateId, string query, int topK = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches across ALL ingested candidate data for a given query.
    /// Used by MAF's <c>TextSearchProvider</c> which provides only a query string (no candidateId).
    /// </summary>
    Task<IEnumerable<(string CandidateId, string Text, double Score)>> SearchAllAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
}
