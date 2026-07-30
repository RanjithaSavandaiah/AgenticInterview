using Microsoft.AspNetCore.Mvc;
using AgenticInterview.AgenticSystem.AgentCards;

namespace AgenticInterview.Api.Controllers;

/// <summary>
/// Implements the A2A (Agent-to-Agent) discovery protocol.
/// Exposes agent cards at the well-known endpoint so external systems
/// and other agents can discover this system's capabilities at runtime.
/// 
/// Conforms to Google's A2A specification:
/// https://google.github.io/A2A/
/// </summary>
[ApiController]
public class AgentDiscoveryController : ControllerBase
{
    private readonly AgentCardRegistry _registry;

    public AgentDiscoveryController(AgentCardRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// A2A well-known discovery endpoint.
    /// Returns the full agent card registry as JSON conforming to the A2A protocol.
    /// </summary>
    [HttpGet("/.well-known/agent.json")]
    [Produces("application/json")]
    public IActionResult GetWellKnownAgentCard()
    {
        var cards = _registry.GetAll();
        var discovery = new
        {
            name = "AgenticInterview",
            description = "A multi-agent AI interview system that conducts, proctors, and evaluates technical and behavioral interviews autonomously.",
            version = AgenticInterview.AgenticSystem.Common.AgenticConstants.A2AProtocolVersion,
            protocol = "a2a",
            agents = cards.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                description = c.Description,
                goal = c.Goal,
                skills = c.Skills.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    description = s.Description,
                    tags = s.Tags
                }),
                inputContentTypes = c.InputContentTypes,
                outputContentTypes = c.OutputContentTypes,
                supportsStreaming = c.SupportsStreaming,
                endpointUrl = c.EndpointUrl
            })
        };

        return Ok(discovery);
    }

    /// <summary>
    /// Lists all registered agent cards.
    /// </summary>
    [HttpGet("api/agents")]
    [Produces("application/json")]
    public IActionResult ListAgents()
    {
        return Ok(_registry.GetAll());
    }

    /// <summary>
    /// Retrieves a specific agent card by its ID.
    /// </summary>
    [HttpGet("api/agents/{id}")]
    [Produces("application/json")]
    public IActionResult GetAgent(string id)
    {
        var card = _registry.GetById(id);
        if (card == null)
            return NotFound(new { error = $"Agent '{id}' not found." });

        return Ok(card);
    }

    /// <summary>
    /// Searches for agents by skill tag.
    /// </summary>
    [HttpGet("api/agents/search")]
    [Produces("application/json")]
    public IActionResult SearchAgents([FromQuery] string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return BadRequest(new { error = "Query parameter 'tag' is required." });

        var results = _registry.FindBySkillTag(tag);
        return Ok(results);
    }
}
