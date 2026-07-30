using System;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.ValueObjects;

namespace AgenticInterview.Domain.Entities;

public class CandidateAnswer : BaseEntity
{
    public string Transcript { get; private set; }
    public EvaluationScore? Score { get; private set; }
    public string? AiFeedback { get; private set; }

    private CandidateAnswer() { Transcript = string.Empty; }

    public CandidateAnswer(string transcript)
    {
        Transcript = transcript;
    }

    public void Evaluate(EvaluationScore score, string feedback)
    {
        Score = score;
        AiFeedback = feedback;
    }
}
