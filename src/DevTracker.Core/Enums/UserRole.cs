namespace DevTracker.Core.Enums;

/// <summary>
/// Define o nível de acesso de um utilizador no sistema.
/// Determina quais operações estão disponíveis via IPermissionService.
/// </summary>
public enum UserRole
{
    /// <summary>Controlo total, incluindo gestão de utilizadores e operações destrutivas.</summary>
    Owner = 0,

    /// <summary>Gestão operacional de projetos e trabalho, sem controlo administrativo total.</summary>
    Admin = 1,

    /// <summary>Manutenção diária de projetos e tarefas, sem operações administrativas sensíveis.</summary>
    Maintainer = 2,

    /// <summary>Acesso apenas de leitura e comentário básico.</summary>
    Reader = 3
}
