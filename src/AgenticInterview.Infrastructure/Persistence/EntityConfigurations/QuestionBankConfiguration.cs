using AgenticInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticInterview.Infrastructure.Persistence.EntityConfigurations;

public class QuestionBankConfiguration : IEntityTypeConfiguration<QuestionBank>
{
    public void Configure(EntityTypeBuilder<QuestionBank> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasMany(x => x.Items)
               .WithOne()
               .HasForeignKey("QuestionBankId")
               .OnDelete(DeleteBehavior.Cascade);
    }
}
