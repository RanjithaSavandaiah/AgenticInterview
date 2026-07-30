using System;
using System.Linq;
using System.Threading.Tasks;
using AgenticInterview.Domain.Entities;
using AgenticInterview.Domain.Enums;
using AgenticInterview.Infrastructure.Persistence;

namespace AgenticInterview.Infrastructure.Data;

public static class QuestionBankSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (context.JobDescriptions.Any()) return; // Already seeded

        var frontendJob = new JobDescriptionProfile("Frontend Developer", TargetJobRole.Frontend, "Seeking an experienced Angular developer.");
        var backendJob = new JobDescriptionProfile("Backend Developer", TargetJobRole.Backend, "Seeking an experienced .NET developer.");

        context.JobDescriptions.AddRange(frontendJob, backendJob);
        
        var questions = new[]
        {
            new QuestionBankItem("What is Dependency Injection?", InterviewQuestionType.Theory, QuestionDifficultyLevel.Medium, ".NET", "Candidate should mention inversion of control and decoupling."),
            new QuestionBankItem("Explain the difference between a class and a struct in C#.", InterviewQuestionType.Theory, QuestionDifficultyLevel.Medium, "C#", "Reference vs Value type, heap vs stack."),
            new QuestionBankItem("Write a function to reverse a string.", InterviewQuestionType.Coding, QuestionDifficultyLevel.Easy, "Algorithms", "O(n) time complexity, correct output."),
            new QuestionBankItem("Tell me about a time you had a conflict with a coworker.", InterviewQuestionType.Behavioral, QuestionDifficultyLevel.Medium, "Soft Skills", "STAR method, positive resolution."),
            new QuestionBankItem("How do you handle performance optimization in an Angular application?", InterviewQuestionType.SystemDesign, QuestionDifficultyLevel.Hard, "Angular", "ChangeDetectionStrategy.OnPush, lazy loading, trackBy.")
        };

        context.QuestionBankItems.AddRange(questions);
        await context.SaveChangesAsync();
    }
}
