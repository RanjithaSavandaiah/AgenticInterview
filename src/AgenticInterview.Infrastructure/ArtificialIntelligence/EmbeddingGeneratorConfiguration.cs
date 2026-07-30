using System;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticInterview.Infrastructure.ArtificialIntelligence;

public static class EmbeddingGeneratorConfiguration
{
    public static IServiceCollection AddAiEmbeddingGenerators(this IServiceCollection services, string geminiApiKey)
    {
        // IEmbeddingGenerator registration will be fully implemented when Vector Store is wired up in Phase 4.
        return services;
    }
}
