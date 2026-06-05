using DevTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Tests.Helpers;

/// <summary>
/// Cria uma BD SQLite in-memory isolada por teste.
/// NUNCA usar EF InMemory Provider — não suporta FK, constraints nem QueryFilters.
/// A SqliteConnection é mantida aberta para preservar a BD durante o teste
/// (SQLite in-memory destrói a BD ao fechar a última connection).
/// O AppDbContext dispõe a conexão quando é ele próprio disposed.
/// </summary>
public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
