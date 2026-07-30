using System;
using AgenticInterview.Domain.Common;

namespace AgenticInterview.Domain.Entities;

public class InterviewPlan : BaseEntity
{
    public Guid InterviewSessionId { get; private set; }
    public string StrategyContent { get; private set; }

    private InterviewPlan() { StrategyContent = string.Empty; }

    public InterviewPlan(Guid interviewSessionId, string strategyContent)
    {
        InterviewSessionId = interviewSessionId;
        StrategyContent = strategyContent;
    }
}
