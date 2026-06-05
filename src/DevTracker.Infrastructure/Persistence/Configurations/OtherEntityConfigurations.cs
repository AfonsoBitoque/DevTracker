using DevTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

/// <summary>Settings para entidades com setup simples agrupadas num único ficheiro.</summary>

public sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(128);
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Author Restrict: não apagar user que tenha comentários
        builder.HasOne(c => c.AuthorUser)
               .WithMany(u => u.Comments)
               .HasForeignKey(c => c.AuthorUserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(64);
        builder.Property(l => l.Color).IsRequired().HasMaxLength(7);
    }
}

public sealed class WorkItemLabelConfiguration : IEntityTypeConfiguration<WorkItemLabel>
{
    public void Configure(EntityTypeBuilder<WorkItemLabel> builder)
    {
        // Chave composta: (WorkItemId, LabelId)
        builder.HasKey(wl => new { wl.WorkItemId, wl.LabelId });
    }
}

public sealed class StateTransitionConfiguration : IEntityTypeConfiguration<StateTransition>
{
    public void Configure(EntityTypeBuilder<StateTransition> builder)
    {
        builder.HasKey(st => st.Id);
        builder.Property(st => st.FromState).HasConversion<string>();
        builder.Property(st => st.ToState).HasConversion<string>();
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<string>();
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(128);

        // Se o user for apagado, o audit fica com UserId=null (preserva o histórico)
        builder.HasOne(a => a.User)
               .WithMany()
               .HasForeignKey(a => a.UserId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.UserId, a.Timestamp });
    }
}
