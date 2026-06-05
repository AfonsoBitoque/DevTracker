using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevTracker.Infrastructure.Persistence;

/// <summary>Factory para criar DbContext em design time (migrations).</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=devtracker_design.db");
        return new AppDbContext(optionsBuilder.Options);
    }
}
