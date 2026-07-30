using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Domain.ValueObjects;
using AgenticInterview.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgenticInterview.Infrastructure.DataSeeding;

/// <summary>
/// Seeds the database with sample candidate profiles and job descriptions
/// for development and demo purposes. Idempotent — only inserts if no data exists.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>
    /// Seeds demo candidate profiles and job descriptions if the database is empty.
    /// </summary>
    public static async Task SeedDemoDataAsync(ApplicationDbContext context)
    {
        if (await context.Set<CandidateProfile>().AnyAsync())
            return;

        var candidate1 = new CandidateProfile(
            "John Doe",
            "john.doe@example.com",
            "Experienced .NET developer with 5 years of experience in microservices, Azure, and clean architecture.");
        candidate1.AddSkill(new TechnicalSkill("C#", "Expert", 5));
        candidate1.AddSkill(new TechnicalSkill("ASP.NET Core", "Advanced", 4));
        candidate1.AddSkill(new TechnicalSkill("Angular", "Intermediate", 3));

        var candidate2 = new CandidateProfile(
            "Jane Smith",
            "jane.smith@example.com",
            "Full-stack developer specializing in React and Node.js. 3 years of experience.");
        candidate2.AddSkill(new TechnicalSkill("JavaScript", "Advanced", 4));
        candidate2.AddSkill(new TechnicalSkill("React", "Advanced", 4));

        context.Set<CandidateProfile>().AddRange(candidate1, candidate2);

        var jobDescription1 = new JobDescriptionProfile(
            "Senior .NET Backend Developer",
            TargetJobRole.Backend,
            "We are looking for an experienced .NET developer to build scalable microservices.");
        jobDescription1.AddRequiredSkill("C#");
        jobDescription1.AddRequiredSkill("ASP.NET Core");

        var jobDescription2 = new JobDescriptionProfile(
            "Senior React Frontend Developer",
            TargetJobRole.Frontend,
            "Looking for a frontend expert with strong React skills.");
        jobDescription2.AddRequiredSkill("React");
        jobDescription2.AddRequiredSkill("JavaScript");

        context.Set<JobDescriptionProfile>().AddRange(jobDescription1, jobDescription2);

        await context.SaveChangesAsync();
    }
}
