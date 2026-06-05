using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Title).IsRequired().HasMaxLength(256);
        builder.Property(w => w.Type).HasConversion<string>();
        builder.Property(w => w.Status).HasConversion<string>();
        builder.Property(w => w.Priority).HasConversion<string>();
        builder.Property(w => w.Difficulty).HasConversion<string>();
        builder.Property(w => w.EstimatedTime).HasConversion<string>();

        // Number é único por projeto (índice composto)
        builder.HasIndex(w => new { w.ProjectId, w.Number }).IsUnique();

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
