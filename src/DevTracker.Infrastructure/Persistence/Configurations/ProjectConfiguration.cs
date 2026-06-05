using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Color).IsRequired().HasMaxLength(7);
        builder.Property(p => p.State).HasConversion<string>();

        // QueryFilter global de soft delete — usar .IgnoreQueryFilters() para ver apagados
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
