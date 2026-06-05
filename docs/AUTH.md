# DevTracker — Authentication & Session Context

> Ficheiro de contexto: autenticação local, sessão em memória, re-autenticação e bootstrap do primeiro utilizador.
> Usar em conjunto com STACK.md e DOMAIN.md.

---

## 1. Objetivos de segurança

A autenticação do DevTracker é **local-first** e **offline-first**.  
Não existe login cloud, OAuth, IdentityServer nem qualquer dependência externa.

Objetivos:
- Garantir que só utilizadores autenticados podem usar operações protegidas
- Exigir permissões por role antes de operações críticas
- Requerer **re-autenticação** para ações destrutivas ou administrativas
- Manter a sessão apenas em memória RAM
- Nunca guardar passwords em plaintext
- Permitir bootstrap seguro do primeiro utilizador `Owner`

---

## 2. Componentes principais

```csharp
IAuthService
ISessionService
IPasswordHasher
ICurrentUserService
IReAuthenticationService
```

### Responsabilidades

- **IAuthService**: login, logout, setup inicial, mudança de password
- **ISessionService**: gerir sessão atual em memória, timeout, lock/unlock
- **IPasswordHasher**: hash e verify com Argon2id
- **ICurrentUserService**: expor utilizador autenticado atual para UI e serviços
- **IReAuthenticationService**: confirmar password antes de operações críticas

---

## 3. Interfaces

### IAuthService

```csharp
// src/DevTracker.Application/Abstractions/Security/IAuthService.cs
using DevTracker.Application.Security.Models;

namespace DevTracker.Application.Abstractions.Security;

public interface IAuthService
{
    Task<AuthResult> SetupFirstOwnerAsync(string username, string password, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<bool> AnyUsersExistAsync(CancellationToken ct = default);
}
```

### ISessionService

```csharp
// src/DevTracker.Application/Abstractions/Security/ISessionService.cs
using DevTracker.Application.Security.Models;

namespace DevTracker.Application.Abstractions.Security;

public interface ISessionService
{
    SessionInfo? Current { get; }
    bool IsAuthenticated { get; }
    bool IsLocked { get; }

    void Start(UserSession session);
    void End();
    void Touch();
    void Lock();
    bool IsExpired();
    bool ValidateToken(Guid token);
}
```

### IPasswordHasher

```csharp
// src/DevTracker.Application/Abstractions/Security/IPasswordHasher.cs
namespace DevTracker.Application.Abstractions.Security;

public interface IPasswordHasher
{
    PasswordHashResult Hash(string password);
    bool Verify(string password, string hash, string salt);
}

public sealed record PasswordHashResult(string Hash, string Salt);
```

### ICurrentUserService

```csharp
// src/DevTracker.Application/Abstractions/Security/ICurrentUserService.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.Abstractions.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
```

### IReAuthenticationService

```csharp
// src/DevTracker.Application/Abstractions/Security/IReAuthenticationService.cs
namespace DevTracker.Application.Abstractions.Security;

public interface IReAuthenticationService
{
    Task<Result> ConfirmPasswordAsync(Guid userId, string password, CancellationToken ct = default);
}
```

---

## 4. Modelos de sessão e auth

```csharp
// src/DevTracker.Application/Security/Models/UserSession.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.Security.Models;

public sealed record UserSession(
    Guid Token,
    Guid UserId,
    string Username,
    UserRole Role,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime LastActivityUtc);
```

```csharp
// src/DevTracker.Application/Security/Models/SessionInfo.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.Security.Models;

public sealed record SessionInfo(
    Guid Token,
    Guid UserId,
    string Username,
    UserRole Role,
    bool IsLocked,
    DateTime ExpiresAtUtc,
    DateTime LastActivityUtc);
```

```csharp
// src/DevTracker.Application/Security/Models/AuthResult.cs
namespace DevTracker.Application.Security.Models;

public sealed record AuthResult(
    bool Succeeded,
    string? Error,
    SessionInfo? Session)
{
    public static AuthResult Success(SessionInfo session) => new(true, null, session);
    public static AuthResult Fail(string error) => new(false, error, null);
}
```

```csharp
// src/DevTracker.Application/Common/Result.cs
namespace DevTracker.Application.Common;

public sealed record Result(bool Succeeded, string? Error = null)
{
    public static Result Success() => new(true);
    public static Result Fail(string error) => new(false, error);
}
```

---

## 5. Password Hashing — Argon2id

### Regras

- Algoritmo: **Argon2id**
- Salt: aleatório, único por utilizador
- Nunca reutilizar salt
- Nunca armazenar password original
- Nunca comparar strings manualmente fora do hasher

### Implementação sugerida

```csharp
// src/DevTracker.Infrastructure/Security/Argon2PasswordHasher.cs
using DevTracker.Application.Abstractions.Security;
using Isopoh.Cryptography.Argon2;
using System.Security.Cryptography;
using System.Text;

namespace DevTracker.Infrastructure.Security;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    public PasswordHashResult Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var salt = Convert.ToBase64String(saltBytes);

        var config = new Argon2Config
        {
            Type = Argon2Type.ID,
            Version = Argon2Version.Nineteen,
            TimeCost = 4,
            MemoryCost = 65536,
            Lanes = 4,
            Threads = Environment.ProcessorCount > 4 ? 4 : 2,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = Encoding.UTF8.GetBytes(salt),
            HashLength = 32
        };

        using var argon2 = new Argon2(config);
        var hash = config.EncodeString(argon2.Hash());

        return new PasswordHashResult(hash, salt);
    }

    public bool Verify(string password, string hash, string salt)
    {
        return Argon2.Verify(hash, password);
    }
}
```

### Nota importante

Apesar de guardarmos `Salt` separado na entidade `User`, a string codificada do Argon2 também já pode conter metadados e salt.  
**Mantemos o campo `Salt` mesmo assim** porque:
- deixa explícita a arquitetura do modelo
- facilita migração futura para outro hasher
- torna auditoria interna mais clara

Se preferires simplificar depois, podes remover `Salt` da entidade e guardar apenas `PasswordHash` completo.

---

## 6. Sessão em memória

A sessão **não é persistida** em disco.  
Quando a app fecha, a sessão desaparece.

### Implementação

```csharp
// src/DevTracker.Infrastructure/Security/InMemorySessionService.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Security.Models;

namespace DevTracker.Infrastructure.Security;

public sealed class InMemorySessionService : ISessionService
{
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(15);
    private UserSession? _current;
    private bool _isLocked;

    public SessionInfo? Current => _current is null
        ? null
        : new SessionInfo(
            _current.Token,
            _current.UserId,
            _current.Username,
            _current.Role,
            _isLocked,
            _current.ExpiresAtUtc,
            _current.LastActivityUtc);

    public bool IsAuthenticated => _current is not null;
    public bool IsLocked => _isLocked;

    public void Start(UserSession session)
    {
        _current = session;
        _isLocked = false;
    }

    public void End()
    {
        _current = null;
        _isLocked = false;
    }

    public void Touch()
    {
        if (_current is null || _isLocked)
            return;

        if (DateTime.UtcNow - _current.LastActivityUtc > _idleTimeout)
        {
            _isLocked = true;
            return;
        }

        _current = _current with { LastActivityUtc = DateTime.UtcNow };
    }

    public void Lock()
    {
        if (_current is null)
            return;

        _isLocked = true;
    }

    public bool IsExpired()
    {
        if (_current is null)
            return true;

        return DateTime.UtcNow > _current.ExpiresAtUtc;
    }

    public bool ValidateToken(Guid token)
    {
        if (_current is null || _isLocked)
            return false;

        if (IsExpired())
        {
            End();
            return false;
        }

        return _current.Token == token;
    }
}
```

### Regras

- Timeout por inatividade: **15 minutos** por default
- Expiração dura da sessão: **8 horas** por default
- Lock automático quando a app acorda de idle excessivo
- Logout manual limpa tudo da memória

---

## 7. AuthService

```csharp
// src/DevTracker.Infrastructure/Security/AuthService.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Common;
using DevTracker.Application.Security.Models;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Security;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ISessionService sessionService,
    IAuditService auditService) : IAuthService, IReAuthenticationService
{
    public async Task<bool> AnyUsersExistAsync(CancellationToken ct = default)
        => await db.Users.AnyAsync(ct);

    public async Task<AuthResult> SetupFirstOwnerAsync(string username, string password, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
            return AuthResult.Fail("Já existem utilizadores no sistema.");

        var pwd = passwordHasher.Hash(password);

        var user = new User
        {
            Username = username.Trim(),
            PasswordHash = pwd.Hash,
            Salt = pwd.Salt,
            Role = UserRole.Owner,
            IsActive = true,
            LastLoginAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var session = CreateSession(user);
        sessionService.Start(session);

        await auditService.LogAsync(
            user.Id,
            AuditAction.UserCreated,
            nameof(User),
            user.Id,
            oldValue: null,
            newValue: $"{{\"username\":\"{user.Username}\",\"role\":\"Owner\"}}",
            ct);

        await auditService.LogAsync(
            user.Id,
            AuditAction.Login,
            nameof(User),
            user.Id,
            oldValue: null,
            newValue: "Bootstrap first owner login",
            ct);

        return AuthResult.Success(ToSessionInfo(session));
    }

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = username.Trim();

        var user = await db.Users
            .SingleOrDefaultAsync(u => u.Username == normalized && u.IsActive, ct);

        if (user is null)
            return AuthResult.Fail("Credenciais inválidas.");

        var ok = passwordHasher.Verify(password, user.PasswordHash, user.Salt);
        if (!ok)
            return AuthResult.Fail("Credenciais inválidas.");

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var session = CreateSession(user);
        sessionService.Start(session);

        await auditService.LogAsync(
            user.Id,
            AuditAction.Login,
            nameof(User),
            user.Id,
            oldValue: null,
            newValue: $"{{\"username\":\"{user.Username}\"}}",
            ct);

        return AuthResult.Success(ToSessionInfo(session));
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        var userId = sessionService.Current?.UserId;

        sessionService.End();

        if (userId.HasValue)
        {
            await auditService.LogAsync(
                userId,
                AuditAction.Logout,
                nameof(User),
                userId,
                oldValue: null,
                newValue: "Manual logout",
                ct);
        }
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        if (user is null)
            return Result.Fail("Utilizador não encontrado.");

        if (!passwordHasher.Verify(currentPassword, user.PasswordHash, user.Salt))
            return Result.Fail("Password atual inválida.");

        var pwd = passwordHasher.Hash(newPassword);
        var oldHash = user.PasswordHash;

        user.PasswordHash = pwd.Hash;
        user.Salt = pwd.Salt;

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(
            user.Id,
            AuditAction.PasswordChanged,
            nameof(User),
            user.Id,
            oldValue: "Password hash updated",
            newValue: "Password hash updated",
            ct);

        return Result.Success();
    }

    public async Task<Result> ConfirmPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        if (user is null)
            return Result.Fail("Utilizador não encontrado.");

        var ok = passwordHasher.Verify(password, user.PasswordHash, user.Salt);
        return ok ? Result.Success() : Result.Fail("Re-autenticação falhou.");
    }

    private static UserSession CreateSession(User user)
    {
        var now = DateTime.UtcNow;

        return new UserSession(
            Token: Guid.NewGuid(),
            UserId: user.Id,
            Username: user.Username,
            Role: user.Role,
            StartedAtUtc: now,
            ExpiresAtUtc: now.AddHours(8),
            LastActivityUtc: now);
    }

    private static SessionInfo ToSessionInfo(UserSession session)
        => new(
            session.Token,
            session.UserId,
            session.Username,
            session.Role,
            false,
            session.ExpiresAtUtc,
            session.LastActivityUtc);
}
```

---

## 8. CurrentUserService

Este serviço evita que a UI tenha de conhecer diretamente o `ISessionService`.

```csharp
// src/DevTracker.Infrastructure/Security/CurrentUserService.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Core.Enums;

namespace DevTracker.Infrastructure.Security;

public sealed class CurrentUserService(ISessionService sessionService) : ICurrentUserService
{
    public Guid? UserId => sessionService.Current?.UserId;
    public string? Username => sessionService.Current?.Username;
    public UserRole? Role => sessionService.Current?.Role;
    public bool IsAuthenticated => sessionService.IsAuthenticated && !sessionService.IsLocked;
}
```

---

## 9. Audit logging esperado

O `AuthService` deve registar sempre:

- `UserCreated` no bootstrap do primeiro owner
- `Login`
- `Logout`
- `PasswordChanged`

Nunca registar:
- plaintext password
- password hash completo em logs de texto
- salt em logs
- token da sessão em ficheiros de log

---

## 10. Fluxo da primeira execução

### Cenário

Na primeira vez que a app abre, a BD está vazia e não existem utilizadores.

### Fluxo

1. `Program.cs` aplica migrações
2. `IAuthService.AnyUsersExistAsync()` retorna `false`
3. UI mostra `FirstRunSetupView`
4. O utilizador define:
   - `Username`
   - `Password`
   - `ConfirmPassword`
5. UI chama `SetupFirstOwnerAsync()`
6. O sistema cria o primeiro utilizador com role `Owner`
7. O sistema inicia sessão automaticamente
8. UI navega para o dashboard principal

### Regras

- Só pode existir **um bootstrap** inicial
- Depois de existir qualquer utilizador, `SetupFirstOwnerAsync()` falha sempre
- O primeiro `Owner` só pode ser removido se existir outro `Owner` primeiro

---

## 11. Fluxo de re-autenticação

Ações que devem exigir password de confirmação:

- Apagar projeto
- Apagar repositório físico
- Promover/demover utilizador
- Alterar password do próprio user
- Exportar ou abrir segredos sensíveis futuros

### Fluxo

1. UI abre `ReAuthenticateDialog`
2. O utilizador escreve a password atual
3. UI chama `ConfirmPasswordAsync(currentUserId, password)`
4. Se válido, a ação crítica é libertada por uma janela curta (ex: 30 segundos)

### ICriticalActionTokenService

Implementação recomendada para evitar passar a password plaintext até ao serviço destrutivo:

```csharp
// src/DevTracker.Application/Abstractions/Security/ICriticalActionTokenService.cs
namespace DevTracker.Application.Abstractions.Security;

public interface ICriticalActionTokenService
{
    /// <summary>
    /// Cria um token de curta duração após re-autenticação bem-sucedida.
    /// </summary>
    Guid CreateToken(Guid userId, string permission, TimeSpan? validity = null);

    /// <summary>
    /// Valida e consome o token. Só pode ser usado uma vez.
    /// </summary>
    bool ConsumeToken(Guid token, Guid userId, string permission);
}
```

```csharp
// src/DevTracker.Infrastructure/Security/InMemoryCriticalActionTokenService.cs
using DevTracker.Application.Abstractions.Security;

namespace DevTracker.Infrastructure.Security;

public sealed class InMemoryCriticalActionTokenService : ICriticalActionTokenService
{
    private sealed record TokenEntry(
        Guid UserId,
        string Permission,
        DateTime ExpiresAtUtc);

    private readonly Dictionary<Guid, TokenEntry> _tokens = [];
    private readonly TimeSpan _defaultValidity = TimeSpan.FromSeconds(30);

    public Guid CreateToken(Guid userId, string permission, TimeSpan? validity = null)
    {
        PurgeExpired();

        var token = Guid.NewGuid();
        var entry = new TokenEntry(
            userId,
            permission,
            DateTime.UtcNow.Add(validity ?? _defaultValidity));

        _tokens[token] = entry;
        return token;
    }

    public bool ConsumeToken(Guid token, Guid userId, string permission)
    {
        if (!_tokens.TryGetValue(token, out var entry))
            return false;

        _tokens.Remove(token); // consumido: single-use

        if (entry.UserId != userId) return false;
        if (entry.Permission != permission) return false;
        if (DateTime.UtcNow > entry.ExpiresAtUtc) return false;

        return true;
    }

    private void PurgeExpired()
    {
        var expired = _tokens
            .Where(kv => DateTime.UtcNow > kv.Value.ExpiresAtUtc)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expired)
            _tokens.Remove(key);
    }
}
```

### Fluxo com ICriticalActionTokenService

1. UI chama `IReAuthenticationService.ConfirmPasswordAsync(userId, password)`
2. Se válido, UI chama `ICriticalActionTokenService.CreateToken(userId, permission, 30s)`
3. UI passa o token `Guid` ao serviço destrutivo
4. Serviço chama `ConsumeToken(token, userId, permission)` antes de executar
5. Token é consumido: single-use, expira em 30 segundos

---

## 12. Validações recomendadas

### Login

- Username: obrigatório, 3-64 chars
- Password: obrigatória

### Setup inicial / mudança de password

- mínimo 12 caracteres
- pelo menos 1 maiúscula
- pelo menos 1 minúscula
- pelo menos 1 número
- pelo menos 1 símbolo
- não aceitar passwords demasiado comuns
- `ConfirmPassword` tem de bater certo

Estas regras devem ficar em `FluentValidation` no projeto `Application`.

---

## 13. Dependency Injection

```csharp
// src/DevTracker.Infrastructure/DependencyInjection.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Infrastructure;

public static partial class DependencyInjection
{
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddSingleton<ISessionService, InMemorySessionService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<ICriticalActionTokenService, InMemoryCriticalActionTokenService>();
        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReAuthenticationService, AuthService>();

        return services;
    }
}
```

### Nota sobre lifetime

- `ISessionService` deve ser **Singleton** porque representa a sessão viva da app
- `IAuthService` pode ser `Scoped`
- `IPasswordHasher` pode ser `Scoped` ou `Singleton`; aqui fica `Scoped` por consistência
- `ICriticalActionTokenService` deve ser **Singleton** (estado em memória partilhado)

> **Atenção — Captive Dependency:** `AuthService` é `Scoped` e depende de `ISessionService` (Singleton).
> Esta combinação é segura: um Scoped pode depender de um Singleton.
> O problema seria o inverso: um Singleton não deve depender de um Scoped (captive dependency).
> `ICurrentUserService` é Singleton e depende de `ISessionService` (Singleton) — correto.
> Nunca injetar `AppDbContext` (Scoped) num Singleton diretamente; usar `IServiceScopeFactory` se necessário.

---

## 14. Program.cs — bootstrap de auth

```csharp
// Exemplo simplificado
var services = new ServiceCollection();

services
    .AddInfrastructure(dbPath)
    .AddSecurity();

var provider = services.BuildServiceProvider();

using var scope = provider.CreateScope();
var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

var hasUsers = await auth.AnyUsersExistAsync();

if (!hasUsers)
{
    // Navegar para FirstRunSetupView
}
else
{
    // Navegar para LoginView
}
```

---

## 15. Melhorias futuras

- Proteção adicional com keyring nativo por SO
- Suporte a múltiplas sessões internas por perfil
- 2FA local com TOTP offline (opcional)
- Políticas de rotação de password
- Rate limiting local após várias tentativas falhadas
- Secure wipe de strings sensíveis onde possível

---

## 16. Regras finais

1. A UI nunca valida permissões de forma autoritativa — apenas melhora UX. A decisão real acontece nos serviços.
2. A sessão vive só em RAM.
3. O primeiro owner nasce apenas uma vez.
4. Re-autenticação é obrigatória para ações destrutivas.
5. Toda a autenticação deve deixar rasto em `AuditEntry`.

