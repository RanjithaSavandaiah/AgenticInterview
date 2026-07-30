using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;

namespace AgenticInterview.Application.Abstractions;

/// <summary>
/// Contract for retrieving questions from the question bank with caching.
/// The implementation handles the caching strategy internally.
/// </summary>
public interface ICachedQuestionBankService
{
    /// <summary>
    /// Retrieves questions filtered by difficulty and type, with caching.
    /// </summary>
    Task<IReadOnlyList<QuestionBankItem>> GetQuestionsAsync(
        QuestionDifficultyLevel? difficulty = null,
        InterviewQuestionType? type = null,
        CancellationToken cancellationToken = default);
}
