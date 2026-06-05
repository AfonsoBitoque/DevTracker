using DevTracker.Core.Enums;

namespace DevTracker.Core.Entities;

/// <summary>
/// Registo imutável de auditoria. NÃO herda BaseEntity — só INSERT, nunca UPDATE/DELETE.
/// UserId é nullable: se o utilizador for apagado, o audit fica com UserId=null (SetNull).
/// OldValue/NewValue devem conter JSON estruturado, nunca passwords ou tokens.
/// </summary>
public class AuditEntry
{
    public Guid         Id          { get; set; } = Guid.NewGuid();
    public Guid?        UserId      { get; set; }
    public AuditAction  Action      { get; set; }
    public string       EntityType  { get; set; } = string.Empty;
    public Guid?        EntityId    { get; set; }
    public string?      OldValue    { get; set; }
    public string?      NewValue    { get; set; }
    public DateTime     Timestamp   { get; set; } = DateTime.UtcNow;
    public string?      MachineName { get; set; } = Environment.MachineName;

    // Navegação (nullable — utilizador pode ter sido apagado)
    public User? User { get; set; }
}
