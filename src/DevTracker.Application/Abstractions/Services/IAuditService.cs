using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Audit;
using DevTracker.Core.Enums;

namespace DevTracker.Application.Abstractions.Services;

/// <summary>
/// Registo imutável de auditoria. Só expõe INSERT (LogAsync) e leitura.
/// Nunca expor Update ou Delete — AuditEntry é imutável por design.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        Guid?       userId,
        AuditAction action,
        string      entityType,
        Guid?       entityId,
        string?     oldValue,
        string?     newValue,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<AuditEntryDto>>> GetRecentAsync(int count = 100, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditEntryDto>>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AuditEntryDto>>> GetByUserAsync(Guid userId, CancellationToken ct = default);
}
