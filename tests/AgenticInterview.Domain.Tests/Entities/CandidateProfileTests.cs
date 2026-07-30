using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.ValueObjects;
using Xunit;

namespace AgenticInterview.Domain.Tests.Entities;

public class CandidateProfileTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var name = "Jane Doe";
        var email = "jane@example.com";
        var resume = "Experienced developer.";

        // Act
        var candidate = new CandidateProfile(name, email, resume);

        // Assert
        Assert.Equal(name, candidate.Name);
        Assert.Equal(email, candidate.Email);
        Assert.Equal(resume, candidate.ResumeTextContent);
        Assert.Empty(candidate.Skills);
    }

    [Fact]
    public void AddSkill_AddsSkillToList()
    {
        // Arrange
        var candidate = new CandidateProfile("Jane Doe", "jane@example.com", "Resume");
        var skill = new TechnicalSkill("C#", "Expert", 90);

        // Act
        candidate.AddSkill(skill);

        // Assert
        Assert.Single(candidate.Skills);
        Assert.Contains(skill, candidate.Skills);
    }
}
