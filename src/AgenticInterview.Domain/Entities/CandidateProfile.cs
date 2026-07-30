using System;
using System.Collections.Generic;
using AgenticInterview.Domain.Common;
using AgenticInterview.Domain.ValueObjects;
using AgenticInterview.Domain.Interfaces;

namespace AgenticInterview.Domain.Entities;

public class CandidateProfile : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string ResumeTextContent { get; private set; }
    
    private readonly List<TechnicalSkill> _skills = new();
    public IReadOnlyCollection<TechnicalSkill> Skills => _skills.AsReadOnly();

    private CandidateProfile() { Name = string.Empty; Email = string.Empty; ResumeTextContent = string.Empty; }

    public CandidateProfile(string name, string email, string resumeTextContent)
    {
        Name = name;
        Email = email;
        ResumeTextContent = resumeTextContent;
    }

    public void AddSkill(TechnicalSkill skill)
    {
        _skills.Add(skill);
    }
}
