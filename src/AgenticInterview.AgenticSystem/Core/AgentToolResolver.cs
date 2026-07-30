using System.Collections.Generic;
using System.Linq;
using AgenticInterview.AgenticSystem.AgentCards;
using AgenticInterview.AgenticSystem.McpTools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.AgenticSystem.Core;

/// <summary>
/// Resolves the appropriate MCP tools for each agent based on its agent card skills.
/// This ensures agents only receive tools matching their declared capabilities,
/// preventing cross-agent tool misuse and reducing LLM token waste.
///
/// Mapping flow:
///   Agent Name → AgentCard (via registry) → AgentSkill.Id[] → MCP Tool[] (via factory)
/// </summary>
public class AgentToolResolver
{
    private readonly AgentCardRegistry _registry;
    private readonly ILogger<AgentToolResolver> _logger;

    public AgentToolResolver(AgentCardRegistry registry, ILogger<AgentToolResolver> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Resolves tools for a specific agent by matching its card's skill IDs against tool definitions.
    /// Falls back to all tools if the agent has no registered card (defensive behavior).
    /// </summary>
    public IList<AITool> ResolveToolsForAgent(
        string agentName,
        ILogger toolLogger,
        System.IServiceProvider serviceProvider)
    {
        // Find the agent card by name
        var card = _registry.GetAll().FirstOrDefault(c => c.Name == agentName);

        if (card == null || card.Skills.Count == 0)
        {
            _logger.LogWarning(
                "No agent card or skills found for agent '{AgentName}'. Providing all tools (fallback).",
                agentName);
            return InterviewMcpToolFactory.CreateAllTools(toolLogger, serviceProvider);
        }

        var skillIds = card.Skills.Select(s => s.Id).ToList();
        var tools = InterviewMcpToolFactory.CreateToolsForSkills(toolLogger, serviceProvider, skillIds);

        _logger.LogInformation(
            "Resolved {ToolCount} tools for agent '{AgentName}' based on skills: [{Skills}]",
            tools.Count, agentName, string.Join(", ", skillIds));

        return tools;
    }
}
