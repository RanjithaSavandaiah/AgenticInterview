using System;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.Enums;

namespace AgenticInterview.Domain.Entities;

public class ProctoringIncident : BaseEntity
{
    public ProctoringViolationType Type { get; private set; }
    public string AgentReasoning { get; private set; }
    public bool IsConsideredStrike { get; private set; }

    private ProctoringIncident() { AgentReasoning = string.Empty; }

    public ProctoringIncident(ProctoringViolationType type, string agentReasoning, bool isConsideredStrike)
    {
        Type = type;
        AgentReasoning = agentReasoning;
        IsConsideredStrike = isConsideredStrike;
    }
}
