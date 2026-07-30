using System;
using System.Collections.Generic;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.Interfaces;

namespace AgenticInterview.Domain.Entities;

public class JobDescriptionProfile : BaseEntity, IAggregateRoot
{
    public string Title { get; private set; }
    public TargetJobRole Role { get; private set; }
    public string DescriptionTextContent { get; private set; }

    private readonly List<string> _requiredSkills = new();
    public IReadOnlyCollection<string> RequiredSkills => _requiredSkills.AsReadOnly();

    private JobDescriptionProfile() { Title = string.Empty; DescriptionTextContent = string.Empty; }

    public JobDescriptionProfile(string title, TargetJobRole role, string descriptionTextContent)
    {
        Title = title;
        Role = role;
        DescriptionTextContent = descriptionTextContent;
    }

    public void AddRequiredSkill(string skill)
    {
        _requiredSkills.Add(skill);
    }
}
