using System.Reflection;
using AgenticInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticInterview.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<InterviewSession> InterviewSessions => Set<InterviewSession>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<JobDescriptionProfile> JobDescriptions => Set<JobDescriptionProfile>();
    public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();
    public DbSet<QuestionBankItem> QuestionBankItems => Set<QuestionBankItem>();
    public DbSet<InterviewPlan> InterviewPlans => Set<InterviewPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
