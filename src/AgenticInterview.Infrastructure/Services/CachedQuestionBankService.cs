using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Application.Abstractions;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.Infrastructure.Services;

/// <summary>
/// Provides cached access to the QuestionBank.
/// Questions are static reference data that rarely changes, making them
/// an ideal candidate for in-memory caching. This avoids repeated SQLite
/// round-trips on every agent orchestration cycle.
/// </summary>
public class CachedQuestionBankService : ICachedQuestionBankService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedQuestionBankService> _logger;

    /// <summary>
    /// Cache key prefix for question bank entries.
    /// </summary>
    private const string CacheKeyPrefix = "QuestionBank";

    /// <summary>
    /// Cache duration — questions are static, so 30 minutes is safe.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public CachedQuestionBankService(
        ApplicationDbContext dbContext,
        IMemoryCache cache,
        ILogger<CachedQuestionBankService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuestionBankItem>> GetQuestionsAsync(
        QuestionDifficultyLevel? difficulty = null,
        InterviewQuestionType? type = null,
        CancellationToken cancellationToken = default)
    {
        // Build a cache key that includes the filter parameters
        var cacheKey = $"{CacheKeyPrefix}_{difficulty?.ToString() ?? "All"}_{type?.ToString() ?? "All"}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<QuestionBankItem>? cachedQuestions) && cachedQuestions != null)
        {
            _logger.LogDebug("QuestionBank cache HIT for key: {CacheKey}. Returning {Count} cached questions.", cacheKey, cachedQuestions.Count);
            return cachedQuestions;
        }

        _logger.LogInformation("QuestionBank cache MISS for key: {CacheKey}. Querying database...", cacheKey);

        // Build the query
        IQueryable<QuestionBankItem> query = _dbContext.QuestionBankItems;

        if (difficulty.HasValue)
            query = query.Where(q => q.Difficulty == difficulty.Value);

        if (type.HasValue)
            query = query.Where(q => q.Type == type.Value);

        var questions = await query.ToListAsync(cancellationToken);

        // Store in cache with sliding + absolute expiration
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10))
            .SetAbsoluteExpiration(CacheDuration)
            .SetSize(questions.Count); // For bounded cache sizing

        _cache.Set(cacheKey, (IReadOnlyList<QuestionBankItem>)questions.AsReadOnly(), cacheOptions);

        _logger.LogInformation("Cached {Count} questions under key: {CacheKey}", questions.Count, cacheKey);

        return questions.AsReadOnly();
    }
}
