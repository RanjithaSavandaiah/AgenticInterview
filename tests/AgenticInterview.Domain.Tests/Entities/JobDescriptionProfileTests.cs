using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using Xunit;

namespace AgenticInterview.Domain.Tests.Entities;

public class JobDescriptionProfileTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var title = "Senior Backend Engineer";
        var role = TargetJobRole.Backend;
        var description = "Looking for a C# expert with ASP.NET Core experience.";

        // Act
        var job = new JobDescriptionProfile(title, role, description);

        // Assert
        Assert.Equal(title, job.Title);
        Assert.Equal(role, job.Role);
        Assert.Equal(description, job.DescriptionTextContent);
        Assert.Empty(job.RequiredSkills);
    }

    [Fact]
    public void AddRequiredSkill_AddsSkillToList()
    {
        // Arrange
        var job = new JobDescriptionProfile("Dev", TargetJobRole.Backend, "Desc");
        var skill = "C#";

        // Act
        job.AddRequiredSkill(skill);

        // Assert
        Assert.Single(job.RequiredSkills);
        Assert.Contains(skill, job.RequiredSkills);
    }
}
