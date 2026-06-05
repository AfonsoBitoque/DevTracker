using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevTracker.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(64);
        builder.HasIndex(u => u.Username).IsUnique();

        // Enum guardado como string para legibilidade no SQLite
        builder.Property(u => u.Role).HasConversion<string>();

        // WorkItems criados por este user: não apagar o user se tiver work items (Restrict)
        builder.HasMany(u => u.CreatedWorkItems)
               .WithOne(w => w.CreatedByUser)
               .HasForeignKey(w => w.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        // WorkItems atribuídos: se o user for apagado, fica sem atribuição (SetNull)
        builder.HasMany(u => u.AssignedWorkItems)
               .WithOne(w => w.AssignedToUser)
               .HasForeignKey(w => w.AssignedToUserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
