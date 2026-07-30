using AgenticInterview.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgenticInterview.Infrastructure.Persistence.EntityConfigurations;

public class ProctoringIncidentConfiguration : IEntityTypeConfiguration<ProctoringIncident>
{
    public void Configure(EntityTypeBuilder<ProctoringIncident> builder)
    {
        builder.HasKey(x => x.Id);
    }
}
