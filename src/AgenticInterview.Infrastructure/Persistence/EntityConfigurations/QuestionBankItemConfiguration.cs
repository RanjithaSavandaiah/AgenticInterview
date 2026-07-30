using AgenticInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticInterview.Infrastructure.Persistence.EntityConfigurations;

public class QuestionBankItemConfiguration : IEntityTypeConfiguration<QuestionBankItem>
{
    public void Configure(EntityTypeBuilder<QuestionBankItem> builder)
    {
        builder.HasKey(x => x.Id);
    }
}
