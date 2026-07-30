using System;
using System.Collections.Generic;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.Enums;

namespace AgenticInterview.Domain.Entities;

public class QuestionBank : BaseEntity
{
    public string Name { get; private set; }
    public TargetJobRole Role { get; private set; }

    private readonly List<QuestionBankItem> _items = new();
    public IReadOnlyCollection<QuestionBankItem> Items => _items.AsReadOnly();

    private QuestionBank() { Name = string.Empty; }

    public QuestionBank(string name, TargetJobRole role)
    {
        Name = name;
        Role = role;
    }

    public void AddItem(QuestionBankItem item)
    {
        _items.Add(item);
    }
}
