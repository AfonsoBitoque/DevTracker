using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Audit;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

/// <summary>
/// Registo imutável de auditoria. Só expõe INSERT + leitura.
/// Nunca atualizar nem apagar AuditEntry — é um log permanente.
/// </summary>
public sealed class AuditService(AppDbContext db) : IAuditService
{
    public async Task LogAsync(
        Guid?       userId,
        AuditAction action,
        string      entityType,
        Guid?       entityId,
        string?     oldValue,
        string?     newValue,
        CancellationToken ct = default)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            UserId     = userId,
            Action     = action,
            EntityType = entityType,
            EntityId   = entityId,
            OldValue   = oldValue,
            NewValue   = newValue
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> GetRecentAsync(int count = 100, CancellationToken ct = default)
    {
        var entries = await BaseQuery()
            .OrderByDescending(a => a.Timestamp).Take(count)
            .Select(ToDto).ToListAsync(ct);
        return Result<IReadOnlyList<AuditEntryDto>>.Success(entries);
    }

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        var entries = await BaseQuery()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .Select(ToDto).ToListAsync(ct);
        return Result<IReadOnlyList<AuditEntryDto>>.Success(entries);
    }

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var entries = await BaseQuery()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Select(ToDto).ToListAsync(ct);
        return Result<IReadOnlyList<AuditEntryDto>>.Success(entries);
    }

    /// <summary>Query base sem filtros — projeção separada para manter filtragem sobre entidades.</summary>
    private IQueryable<AuditEntry> BaseQuery()
        => db.AuditEntries.AsNoTracking().Include(a => a.User);

    private static readonly System.Linq.Expressions.Expression<Func<AuditEntry, AuditEntryDto>> ToDto =
        a => new AuditEntryDto(
            a.Id, a.User != null ? a.User.Username : null,
            a.Action, a.EntityType, a.EntityId,
            a.OldValue, a.NewValue, a.Timestamp, a.MachineName);
}
