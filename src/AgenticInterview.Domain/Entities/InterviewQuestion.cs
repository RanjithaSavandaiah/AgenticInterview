using System;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.ValueObjects;

namespace AgenticInterview.Domain.Entities;

public class InterviewQuestion : BaseEntity
{
    public string Content { get; private set; }
    public InterviewQuestionType Type { get; private set; }
    public QuestionDifficultyLevel Difficulty { get; private set; }
    public string ExpectedAnswerCriteria { get; private set; }
    public CandidateAnswer? Answer { get; private set; }

    private InterviewQuestion() 
    { 
        Content = string.Empty; 
        ExpectedAnswerCriteria = string.Empty; 
    }

    public InterviewQuestion(string content, InterviewQuestionType type, QuestionDifficultyLevel difficulty, string expectedAnswerCriteria)
    {
        Content = content;
        Type = type;
        Difficulty = difficulty;
        ExpectedAnswerCriteria = expectedAnswerCriteria;
    }

    public void RecordAnswer(CandidateAnswer answer)
    {
        Answer = answer;
    }
}
