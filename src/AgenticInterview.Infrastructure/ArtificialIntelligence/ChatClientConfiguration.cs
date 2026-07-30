using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using AgenticInterview.Infrastructure.Services;

namespace AgenticInterview.Infrastructure.ArtificialIntelligence;

public static class ChatClientConfiguration
{
    public static IServiceCollection AddAiChatClients(this IServiceCollection services, string geminiApiKey, string groqApiKey)
    {
        services.AddScoped<IChatClient>(sp => 
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FallbackChatClient>>();
            
            // Gemini via OpenAI compatible endpoint
            if (string.IsNullOrEmpty(geminiApiKey)) throw new InvalidOperationException("Gemini API Key missing");
            var geminiClient = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(geminiApiKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") })
                .GetChatClient("gemini-2.5-flash");
            IChatClient primary = geminiClient.AsIChatClient();

            // Groq via OpenAI compatible endpoint
            if (string.IsNullOrEmpty(groqApiKey)) throw new InvalidOperationException("Groq API Key missing");
            var groqClient = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(groqApiKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1/") })
                .GetChatClient("llama-3.1-8b-instant");
            IChatClient fallback = groqClient.AsIChatClient();

            // Layer 1 (innermost): Fallback — Gemini primary → Groq secondary
            IChatClient fallbackClient = new FallbackChatClient(primary, fallback, logger);

            // Layer 2: Response caching — deduplicates rapid retries (5-min TTL)
            var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            var cacheLogger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedChatClientDecorator>>();
            var cachedClient = new CachedChatClientDecorator(fallbackClient, cache, cacheLogger);
            
            // Layer 3+4+5 (outermost): ChatClientBuilder middleware pipeline
            // The pipeline executes top-to-bottom on the way IN and bottom-to-top on the way OUT:
            //   Request → OpenTelemetry → Logging → FunctionInvocation → CachedClient → FallbackClient → LLM
            //   Response ← OpenTelemetry ← Logging ← FunctionInvocation ← CachedClient ← FallbackClient ← LLM
            return new ChatClientBuilder(cachedClient)
                .UseOpenTelemetry(
                    sourceName: "AgenticInterview.AI",
                    configure: otel =>
                    {
                        // Emit detailed telemetry: model name, token counts, finish reason per call
                        otel.EnableSensitiveData = false; // Don't log prompt/completion content in production
                    })
                .UseLogging(
                    configure: log =>
                    {
                        // Structured logs for every LLM call: model, token usage, latency
                        // Sensitive data (prompt content) is disabled by default — enable only in dev
                        log.JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = false
                        };
                    })
                .UseFunctionInvocation()
                .Build();
        });

        return services;
    }
}
