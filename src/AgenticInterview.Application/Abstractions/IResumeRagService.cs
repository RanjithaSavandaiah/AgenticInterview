using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticInterview.Application.Abstractions;

public interface IResumeRagService
{
    Task IngestResumeAsync(string candidateId, string resumeText, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> SearchCandidateExperienceAsync(string candidateId, string query, int topK = 3, CancellationToken cancellationToken = default);
}
