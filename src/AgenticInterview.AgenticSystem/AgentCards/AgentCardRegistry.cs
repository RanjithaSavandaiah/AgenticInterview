using System.Collections.Generic;
using System.Linq;
using AgenticInterview.AgenticSystem.Agents;
using AgenticInterview.AgenticSystem.Common;

namespace AgenticInterview.AgenticSystem.AgentCards;

/// <summary>
/// Registry that holds all Agent Cards in the system.
/// Provides A2A-style agent discovery so agents can find and delegate to each other.
/// </summary>
public class AgentCardRegistry
{
    private readonly Dictionary<string, AgentCard> _cards = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers an agent card in the registry.
    /// </summary>
    public void Register(AgentCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _cards[card.Id] = card;
    }

    /// <summary>
    /// Retrieves an agent card by its unique identifier.
    /// </summary>
    public AgentCard? GetById(string agentId)
    {
        _cards.TryGetValue(agentId, out var card);
        return card;
    }

    /// <summary>
    /// Returns all registered agent cards.
    /// </summary>
    public IReadOnlyCollection<AgentCard> GetAll() => _cards.Values.ToList().AsReadOnly();

    /// <summary>
    /// Searches for agents that have a skill matching the given tag.
    /// </summary>
    public IEnumerable<AgentCard> FindBySkillTag(string tag)
    {
        return _cards.Values.Where(c =>
            c.Skills.Any(s => s.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Creates and registers the default agent cards for the interview system.
    /// </summary>
    public static AgentCardRegistry CreateDefault()
    {
        var registry = new AgentCardRegistry();

        registry.Register(new AgentCard
        {
            Id = "technical-interviewer",
            Name = AgenticConstants.TechnicalInterviewerName,
            Description = "Conducts technical interviews with adaptive difficulty. Asks coding, system design, and theory questions.",
            Goal = "Evaluate the candidate's technical proficiency through progressively challenging questions.",
            Skills =
            [
                new AgentSkill { Id = "ask-technical-question", Name = "Ask Technical Question", Description = "Generates context-aware technical questions.", Tags = ["interview", "technical", "coding"] },
                new AgentSkill { Id = "adaptive-difficulty", Name = "Adaptive Difficulty", Description = "Adjusts question difficulty based on candidate performance.", Tags = ["adaptive", "ai"] }
            ],
            SupportsStreaming = true,
            CanDelegateTo = ["code-execution", "web-search"]
        });

        registry.Register(new AgentCard
        {
            Id = "behavioral-interviewer",
            Name = AgenticConstants.BehavioralInterviewerName,
            Description = "Evaluates soft skills, communication, and cultural fit using STAR-method questions.",
            Goal = "Assess the candidate's behavioral competencies and team compatibility.",
            Skills =
            [
                new AgentSkill { Id = "behavioral-question", Name = "Behavioral Question", Description = "Generates STAR-method behavioral questions.", Tags = ["interview", "behavioral", "soft-skills"] }
            ]
        });

        registry.Register(new AgentCard
        {
            Id = "code-execution",
            Name = AgenticConstants.CodeExecutionAgentName,
            Description = "Performs static analysis and evaluates candidate-submitted code for correctness and quality.",
            Goal = "Validate code submissions through static analysis and pattern detection.",
            Skills =
            [
                new AgentSkill { Id = "static-analysis", Name = "Static Code Analysis", Description = "Analyzes code for common patterns, errors, and quality.", Tags = ["code", "analysis", "evaluation"] }
            ]
        });

        registry.Register(new AgentCard
        {
            Id = "proctoring",
            Name = AgenticConstants.ProctoringAgentName,
            Description = "Monitors the interview session for malpractice events such as tab switches, copy/paste, and window blurs.",
            Goal = "Ensure interview integrity by detecting and logging suspicious behavior.",
            Skills =
            [
                new AgentSkill { Id = "detect-malpractice", Name = "Malpractice Detection", Description = "Detects tab switches, copy/paste, and other violations.", Tags = ["proctoring", "security", "monitoring"] }
            ]
        });

        registry.Register(new AgentCard
        {
            Id = "evaluation",
            Name = AgenticConstants.EvaluationAgentName,
            Description = "Aggregates all agent assessments into a final composite score and recommendation.",
            Goal = "Produce a fair, comprehensive evaluation of the candidate.",
            Skills =
            [
                new AgentSkill { Id = "score-aggregation", Name = "Score Aggregation", Description = "Compiles scores from all agents into a final assessment.", Tags = ["evaluation", "scoring"] }
            ],
            CanDelegateTo = ["code-execution", "hr-observer"]
        });

        registry.Register(new AgentCard
        {
            Id = "moderator",
            Name = AgenticConstants.ModeratorAgentName,
            Description = "Orchestrates agent turn-taking and ensures smooth interview flow.",
            Goal = "Coordinate agent interactions and manage conversation flow.",
            Skills =
            [
                new AgentSkill { Id = "orchestration", Name = "Agent Orchestration", Description = "Manages turn-taking and transitions between agents.", Tags = ["orchestration", "moderation"] }
            ],
            CanDelegateTo = ["technical-interviewer", "behavioral-interviewer"]
        });

        registry.Register(new AgentCard
        {
            Id = "hr-observer",
            Name = AgenticConstants.HrObserverAgentName,
            Description = "Provides a real-time feed of interview progress to the HR dashboard.",
            Goal = "Keep HR stakeholders informed with live updates and summaries.",
            Skills =
            [
                new AgentSkill { Id = "live-summary", Name = "Live Summary", Description = "Generates real-time summaries for the HR dashboard.", Tags = ["hr", "dashboard", "reporting"] }
            ]
        });

        registry.Register(new AgentCard
        {
            Id = "web-search",
            Name = AgenticConstants.WebSearchAgentName,
            Description = "Performs web searches to verify candidate claims or gather contextual information.",
            Goal = "Augment agent knowledge with real-time web data.",
            Skills =
            [
                new AgentSkill { Id = "web-lookup", Name = "Web Lookup", Description = "Searches the web for relevant technical information.", Tags = ["web", "search", "rag"] }
            ]
        });

        return registry;
    }
}
