using AgenticInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticInterview.Infrastructure.Persistence.EntityConfigurations;

public class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.HasKey(x => x.Id);

        builder.OwnsMany(x => x.Skills, skill =>
        {
            skill.ToTable("CandidateSkills");
            skill.Property(s => s.Name).HasColumnName("Name");
            skill.Property(s => s.AssessedProficiencyLevel).HasColumnName("ProficiencyLevel");
            skill.Property(s => s.Score).HasColumnName("Score");
        });
    }
}
