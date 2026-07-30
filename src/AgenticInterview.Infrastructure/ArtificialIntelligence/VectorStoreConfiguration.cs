using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticInterview.Infrastructure.ArtificialIntelligence;

public static class VectorStoreConfiguration
{
    public static IServiceCollection AddVectorStore(this IServiceCollection services, string connectionString)
    {
        // Vector store integration will be implemented in Phase 4
        return services;
    }
}
