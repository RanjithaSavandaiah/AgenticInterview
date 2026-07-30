using System;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.Enums;

namespace AgenticInterview.Domain.Entities;

public class QuestionBankItem : BaseEntity
{
    public string Content { get; private set; }
    public InterviewQuestionType Type { get; private set; }
    public QuestionDifficultyLevel Difficulty { get; private set; }
    public string Topic { get; private set; }
    public string ExpectedAnswerCriteria { get; private set; }

    private QuestionBankItem() { Content = string.Empty; Topic = string.Empty; ExpectedAnswerCriteria = string.Empty; }

    public QuestionBankItem(string content, InterviewQuestionType type, QuestionDifficultyLevel difficulty, string topic, string expectedAnswerCriteria)
    {
        Content = content;
        Type = type;
        Difficulty = difficulty;
        Topic = topic;
        ExpectedAnswerCriteria = expectedAnswerCriteria;
    }
}
