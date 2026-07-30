using AgenticInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Linq;

namespace AgenticInterview.Infrastructure.Persistence.EntityConfigurations;

public class InterviewSessionConfiguration : IEntityTypeConfiguration<InterviewSession>
{
    public void Configure(EntityTypeBuilder<InterviewSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.OwnsOne(x => x.Configuration, conf =>
        {
            conf.Property(c => c.DurationMinutes).HasColumnName("DurationMinutes");
            conf.Property(c => c.StartingDifficulty).HasColumnName("StartingDifficulty");
            conf.Property(c => c.IsProctoringStrict).HasColumnName("IsProctoringStrict");
            conf.Property(c => c.StrikeLimit).HasColumnName("StrikeLimit");
            conf.Property(c => c.AllowedQuestionTypes)
                .HasConversion(
                    v => string.Join(',', v.Select(x => x.ToString())),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => Enum.Parse<AgenticInterview.Domain.Enums.InterviewQuestionType>(s))
                          .ToList()
                )
                .HasColumnName("AllowedQuestionTypes");
        });

        builder.OwnsOne(x => x.DifficultyState, state =>
        {
            state.Property(s => s.CurrentLevel).HasColumnName("CurrentDifficulty");
            state.Property(s => s.RecentPerformanceAverage).HasColumnName("RecentPerformanceAverage");
            state.Property(s => s.QuestionsAskedAtCurrentLevel).HasColumnName("QuestionsAskedAtCurrentLevel");
        });

        builder.OwnsOne(x => x.RecordingMetadata, rm =>
        {
            rm.Property(r => r.FilePath).HasColumnName("RecordingFilePath");
            rm.Property(r => r.Duration).HasColumnName("RecordingDuration");
            rm.Property(r => r.SizeBytes).HasColumnName("RecordingSizeBytes");
        });

        builder.OwnsOne(x => x.FinalScore, fs =>
        {
            fs.Property(s => s.Value).HasColumnName("FinalScore");
        });

        builder.HasMany(x => x.Questions)
               .WithOne()
               .HasForeignKey("InterviewSessionId")
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Incidents)
               .WithOne()
               .HasForeignKey("InterviewSessionId")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
