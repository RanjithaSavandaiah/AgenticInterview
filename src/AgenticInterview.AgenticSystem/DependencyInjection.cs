using Microsoft.Extensions.DependencyInjection;
using AgenticInterview.AgenticSystem.Agents;
using AgenticInterview.AgenticSystem.AgentCards;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.Blackboard;
using AgenticInterview.AgenticSystem.Guardrails;

namespace AgenticInterview.AgenticSystem;

public static class DependencyInjection
{
    public static IServiceCollection AddAgenticSystem(this IServiceCollection services)
    {
        // Register the Orchestrator
        services.AddScoped<InterviewOrchestrator>();

        // Register the Card Registry (singleton — cards are static metadata)
        services.AddSingleton<AgentCardRegistry>(sp => AgentCardRegistry.CreateDefault());

        // Register Guardrails (singleton — stateless validation logic)
        services.AddSingleton<AgentGuardrails>();

        // Register the Tool Resolver (singleton — maps agent card skills to MCP tools)
        services.AddSingleton<AgentToolResolver>();

        // Register the Sub-Agent Delegator (scoped — needs access to the DI scope to resolve agents)
        services.AddScoped<SubAgentDelegator>();

        // Register the Background Service for lifecycle management
        services.AddSingleton<InterviewBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<InterviewBackgroundService>());

        // Register all Agents
        services.AddScoped<IAgent, TechnicalInterviewerAgent>();
        services.AddScoped<IAgent, BehavioralInterviewerAgent>();
        services.AddScoped<IAgent, CodeExecutionAgent>();
        services.AddScoped<IAgent, EvaluationAgent>();
        services.AddScoped<IAgent, HrObserverAgent>();
        services.AddScoped<IAgent, ModeratorAgent>();
        services.AddScoped<IAgent, ProctoringAgent>();
        services.AddScoped<IAgent, WebSearchAgent>();

        return services;
    }
}
