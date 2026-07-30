using AgenticInterview.Application.Abstractions;
using AgenticInterview.Infrastructure.ArtificialIntelligence;
using AgenticInterview.Infrastructure.DocumentIntelligence;
using AgenticInterview.Infrastructure.Persistence;
using AgenticInterview.Infrastructure.Services;
using AgenticInterview.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace AgenticInterview.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register IMemoryCache (built-in, zero external dependencies)
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1024; // Bounded cache to prevent unbounded memory growth
        });

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection"))
                   .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();

        var geminiKey = configuration["AI:GeminiApiKey"] ?? string.Empty;
        var groqKey = configuration["AI:GroqApiKey"] ?? string.Empty;
        
        services.AddAiChatClients(geminiKey, groqKey);
        services.AddAiEmbeddingGenerators(geminiKey);
        
        var vectorDbConnStr = configuration.GetConnectionString("VectorDbConnection") ?? "Data Source=vectorstore.db";
        services.AddVectorStore(vectorDbConnStr);
        

        services.AddScoped<IResumeRagService, AgenticInterview.Infrastructure.RetrievalAugmentedGeneration.ResumeRagService>();
        services.AddScoped<ICachedQuestionBankService, CachedQuestionBankService>();
        services.AddTransient<AgenticInterview.Infrastructure.PromptOptimization.InterviewPromptOptimizer>();
        
        services.AddScoped<IReportGenerator, AgenticInterview.Infrastructure.ReportGeneration.PdfReportGenerator>();
        services.AddTransient<Kernel>();

        return services;
    }
}
