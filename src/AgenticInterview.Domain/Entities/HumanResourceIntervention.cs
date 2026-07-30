using System;
using AgenticInterview.Domain.Common;

namespace AgenticInterview.Domain.Entities;

public class HumanResourceIntervention : BaseEntity
{
    public Guid InterviewSessionId { get; private set; }
    public string InstructionText { get; private set; }
    public bool IsProcessed { get; private set; }

    private HumanResourceIntervention() { InstructionText = string.Empty; }

    public HumanResourceIntervention(Guid interviewSessionId, string instructionText)
    {
        InterviewSessionId = interviewSessionId;
        InstructionText = instructionText;
        IsProcessed = false;
    }

    public void MarkAsProcessed()
    {
        IsProcessed = true;
    }
}
