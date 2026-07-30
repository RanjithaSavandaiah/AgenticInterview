using AgenticInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticInterview.Infrastructure.Persistence.EntityConfigurations;

public class InterviewQuestionConfiguration : IEntityTypeConfiguration<InterviewQuestion>
{
    public void Configure(EntityTypeBuilder<InterviewQuestion> builder)
    {
        builder.HasKey(x => x.Id);

        builder.OwnsOne(x => x.Answer, ans =>
        {
            ans.Property(a => a.Transcript).HasColumnName("AnswerTranscript");
            ans.Property(a => a.AiFeedback).HasColumnName("AnswerAiFeedback");
            
            ans.OwnsOne(a => a.Score, score =>
            {
                score.Property(s => s.Value).HasColumnName("AnswerScore");
            });
        });
    }
}
