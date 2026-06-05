# DevTracker — Application Services Context

> Ficheiro de contexto: interfaces completas, DTOs, validadores FluentValidation e regras de negócio dos serviços de aplicação.
> Usar em conjunto com STACK.md, DOMAIN.md, AUTH.md, PERMISSIONS.md e WORKSPACE.md.

---

## 1. Princípios

- Toda a lógica de negócio vive nos serviços, nunca nos ViewModels ou Views.
- Serviços retornam `Result<T>` ou `Result` — nunca lançam exceções para erros esperados.
- `AuthorizationException` é a única exceção permitida para falhas de autorização inesperadas.
- Todo o método que toca BD ou disco é `async Task<T>`.
- Validação com `FluentValidation` acontece **antes** de qualquer operação de persistência.
- Todo o serviço recebe `ICurrentUserService` para saber quem está a executar a operação.

---

## 2. DTOs

Os DTOs vivem em `DevTracker.Application/DTOs/`.

### Requests

```csharp
// src/DevTracker.Application/DTOs/Projects/CreateProjectRequest.cs
namespace DevTracker.Application.DTOs.Projects;

public sealed record CreateProjectRequest(
    string Name,
    string? Description,
    string Color,
    string WorkspacePath);
```

```csharp
// src/DevTracker.Application/DTOs/Projects/UpdateProjectRequest.cs
namespace DevTracker.Application.DTOs.Projects;

public sealed record UpdateProjectRequest(
    Guid Id,
    string Name,
    string? Description,
    string Color);
```

```csharp
// src/DevTracker.Application/DTOs/Projects/ChangeProjectStateRequest.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Projects;

public sealed record ChangeProjectStateRequest(
    Guid ProjectId,
    ProjectState NewState,
    string? Reason);
```

```csharp
// src/DevTracker.Application/DTOs/WorkItems/CreateWorkItemRequest.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.WorkItems;

public sealed record CreateWorkItemRequest(
    Guid ProjectId,
    string Title,
    string? Description,
    WorkItemType Type,
    WorkItemPriority Priority,
    Guid? AssignedToUserId,
    DateTime? DueDate);
```

```csharp
// src/DevTracker.Application/DTOs/WorkItems/UpdateWorkItemRequest.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.WorkItems;

public sealed record UpdateWorkItemRequest(
    Guid Id,
    string Title,
    string? Description,
    WorkItemType Type,
    WorkItemPriority Priority,
    Guid? AssignedToUserId,
    DateTime? DueDate);
```

```csharp
// src/DevTracker.Application/DTOs/WorkItems/ChangeWorkItemStatusRequest.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.WorkItems;

public sealed record ChangeWorkItemStatusRequest(
    Guid WorkItemId,
    WorkItemStatus NewStatus);
```

```csharp
// src/DevTracker.Application/DTOs/WorkItems/AddCommentRequest.cs
namespace DevTracker.Application.DTOs.WorkItems;

public sealed record AddCommentRequest(
    Guid WorkItemId,
    string Body);
```

```csharp
// src/DevTracker.Application/DTOs/Repositories/CreateRepositoryRequest.cs
namespace DevTracker.Application.DTOs.Repositories;

public sealed record CreateRepositoryRequest(
    Guid ProjectId,
    string Name,
    string? Description);
```

```csharp
// src/DevTracker.Application/DTOs/Repositories/AttachRepositoryRequest.cs
namespace DevTracker.Application.DTOs.Repositories;

public sealed record AttachRepositoryRequest(
    Guid ProjectId,
    string SourceAbsolutePath,
    string? Description);
```

```csharp
// src/DevTracker.Application/DTOs/Users/CreateUserRequest.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Users;

public sealed record CreateUserRequest(
    string Username,
    string Password,
    string ConfirmPassword,
    UserRole Role);
```

```csharp
// src/DevTracker.Application/DTOs/Users/ChangeUserRoleRequest.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Users;

public sealed record ChangeUserRoleRequest(
    Guid TargetUserId,
    UserRole NewRole);
```

### Responses / Summary DTOs

```csharp
// src/DevTracker.Application/DTOs/Projects/ProjectSummaryDto.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Projects;

public sealed record ProjectSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string Color,
    ProjectState State,
    int WorkItemCount,
    int RepositoryCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

```csharp
// src/DevTracker.Application/DTOs/Projects/ProjectDetailDto.cs
using DevTracker.Application.DTOs.Repositories;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Projects;

public sealed record ProjectDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string Color,
    string WorkspacePath,
    ProjectState State,
    IReadOnlyList<RepositorySummaryDto> Repositories,
    IReadOnlyList<WorkItemSummaryDto> WorkItems,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

```csharp
// src/DevTracker.Application/DTOs/WorkItems/WorkItemSummaryDto.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.WorkItems;

public sealed record WorkItemSummaryDto(
    Guid Id,
    int Number,
    string Title,
    WorkItemType Type,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    string? AssignedToUsername,
    DateTime? DueDate,
    DateTime UpdatedAt);
```

```csharp
// src/DevTracker.Application/DTOs/WorkItems/WorkItemDetailDto.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.WorkItems;

public sealed record WorkItemDetailDto(
    Guid Id,
    int Number,
    string Title,
    string? Description,
    WorkItemType Type,
    WorkItemStatus Status,
    WorkItemPriority Priority,
    Guid ProjectId,
    string ProjectName,
    string CreatedByUsername,
    string? AssignedToUsername,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<string> Labels,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

```csharp
// src/DevTracker.Application/DTOs/WorkItems/CommentDto.cs
namespace DevTracker.Application.DTOs.WorkItems;

public sealed record CommentDto(
    Guid Id,
    string AuthorUsername,
    string Body,
    DateTime CreatedAt,
    DateTime? EditedAt);
```

```csharp
// src/DevTracker.Application/DTOs/Repositories/RepositorySummaryDto.cs
namespace DevTracker.Application.DTOs.Repositories;

public sealed record RepositorySummaryDto(
    Guid Id,
    string Name,
    string RelativePath,
    string? Description,
    DateTime CreatedAt);
```

```csharp
// src/DevTracker.Application/DTOs/Users/UserSummaryDto.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Users;

public sealed record UserSummaryDto(
    Guid Id,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt);
```

```csharp
// src/DevTracker.Application/DTOs/Audit/AuditEntryDto.cs
using DevTracker.Core.Enums;

namespace DevTracker.Application.DTOs.Audit;

public sealed record AuditEntryDto(
    Guid Id,
    string? Username,
    AuditAction Action,
    string EntityType,
    Guid? EntityId,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    string? MachineName);
```

---

## 3. Interfaces dos Serviços

### IProjectService

```csharp
// src/DevTracker.Application/Abstractions/Services/IProjectService.cs
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;

namespace DevTracker.Application.Abstractions.Services;

public interface IProjectService
{
    Task<Result<IReadOnlyList<ProjectSummaryDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ProjectDetailDto>> GetByIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdateProjectRequest request, CancellationToken ct = default);
    Task<Result> ChangeStateAsync(ChangeProjectStateRequest request, CancellationToken ct = default);
    Task<Result> ArchiveAsync(Guid projectId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid projectId, CancellationToken ct = default);
}
```

### IRepositoryService

```csharp
// src/DevTracker.Application/Abstractions/Services/IRepositoryService.cs
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Repositories;

namespace DevTracker.Application.Abstractions.Services;

public interface IRepositoryService
{
    Task<Result<IReadOnlyList<RepositorySummaryDto>>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(CreateRepositoryRequest request, CancellationToken ct = default);
    Task<Result<Guid>> AttachAsync(AttachRepositoryRequest request, CancellationToken ct = default);
    Task<Result> RenameAsync(Guid repositoryId, string newName, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid repositoryId, CancellationToken ct = default);
}
```

### IWorkItemService

```csharp
// src/DevTracker.Application/Abstractions/Services/IWorkItemService.cs
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.WorkItems;
using DevTracker.Core.Enums;

namespace DevTracker.Application.Abstractions.Services;

public interface IWorkItemService
{
    Task<Result<IReadOnlyList<WorkItemSummaryDto>>> GetByProjectAsync(
        Guid projectId,
        WorkItemStatus? filterStatus = null,
        WorkItemType? filterType = null,
        CancellationToken ct = default);

    Task<Result<WorkItemDetailDto>> GetByIdAsync(Guid workItemId, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(CreateWorkItemRequest request, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdateWorkItemRequest request, CancellationToken ct = default);
    Task<Result> ChangeStatusAsync(ChangeWorkItemStatusRequest request, CancellationToken ct = default);
    Task<Result> AddCommentAsync(AddCommentRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid workItemId, CancellationToken ct = default);
}
```

### IUserService

```csharp
// src/DevTracker.Application/Abstractions/Services/IUserService.cs
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Users;

namespace DevTracker.Application.Abstractions.Services;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserSummaryDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<UserSummaryDto>> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result> ChangeRoleAsync(ChangeUserRoleRequest request, CancellationToken ct = default);
    Task<Result> DeactivateAsync(Guid userId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, CancellationToken ct = default);
}
```

### IAuditService

```csharp
// src/DevTracker.Application/Abstractions/Services/IAuditService.cs
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Audit;
using DevTracker.Core.Enums;

namespace DevTracker.Application.Abstractions.Services;

public interface IAuditService
{
    Task LogAsync(
        Guid? userId,
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? oldValue,
        string? newValue,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<AuditEntryDto>>> GetRecentAsync(
        int count = 100,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<AuditEntryDto>>> GetByEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<AuditEntryDto>>> GetByUserAsync(
        Guid userId,
        CancellationToken ct = default);
}
```

### ILabelService

```csharp
// src/DevTracker.Application/Abstractions/Services/ILabelService.cs
using DevTracker.Application.Common;
using DevTracker.Core.Entities;

namespace DevTracker.Application.Abstractions.Services;

public interface ILabelService
{
    Task<Result<IReadOnlyList<Label>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(string name, string color, CancellationToken ct = default);
    Task<Result> AttachToWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default);
    Task<Result> DetachFromWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid labelId, CancellationToken ct = default);
}
```

---

## 4. Validadores FluentValidation

Os validadores vivem em `DevTracker.Application/Validators/`.

### CreateProjectRequestValidator

```csharp
// src/DevTracker.Application/Validators/Projects/CreateProjectRequestValidator.cs
using DevTracker.Application.DTOs.Projects;
using FluentValidation;

namespace DevTracker.Application.Validators.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MinimumLength(2).WithMessage("O nome deve ter pelo menos 2 caracteres.")
            .MaximumLength(128).WithMessage("O nome não pode ter mais de 128 caracteres.")
            .Matches(@"^[^/\\:*?""<>|]+$").WithMessage("O nome contém caracteres inválidos.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("A cor é obrigatória.")
            .Matches(@"^#[0-9A-Fa-f]{6}$").WithMessage("A cor deve ser um código hex válido (ex: #4f98a3).");

        RuleFor(x => x.WorkspacePath)
            .NotEmpty().WithMessage("O caminho do workspace é obrigatório.");

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage("A descrição não pode ter mais de 1024 caracteres.")
            .When(x => x.Description is not null);
    }
}
```

### ChangeProjectStateRequestValidator

```csharp
// src/DevTracker.Application/Validators/Projects/ChangeProjectStateRequestValidator.cs
using DevTracker.Application.DTOs.Projects;
using DevTracker.Core.Enums;
using FluentValidation;

namespace DevTracker.Application.Validators.Projects;

public sealed class ChangeProjectStateRequestValidator : AbstractValidator<ChangeProjectStateRequest>
{
    private static readonly HashSet<ProjectState> RequireReason =
        [ProjectState.Paused, ProjectState.Cancelled];

    public ChangeProjectStateRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("É obrigatório indicar a razão para pausar ou cancelar o projeto.")
            .When(x => RequireReason.Contains(x.NewState));
    }
}
```

### CreateWorkItemRequestValidator

```csharp
// src/DevTracker.Application/Validators/WorkItems/CreateWorkItemRequestValidator.cs
using DevTracker.Application.DTOs.WorkItems;
using FluentValidation;

namespace DevTracker.Application.Validators.WorkItems;

public sealed class CreateWorkItemRequestValidator : AbstractValidator<CreateWorkItemRequest>
{
    public CreateWorkItemRequestValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("O projeto é obrigatório.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("O título é obrigatório.")
            .MaximumLength(256).WithMessage("O título não pode ter mais de 256 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(4096).WithMessage("A descrição não pode ter mais de 4096 caracteres.")
            .When(x => x.Description is not null);

        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("A data de entrega deve ser futura.")
            .When(x => x.DueDate.HasValue);
    }
}
```

### AddCommentRequestValidator

```csharp
// src/DevTracker.Application/Validators/WorkItems/AddCommentRequestValidator.cs
using DevTracker.Application.DTOs.WorkItems;
using FluentValidation;

namespace DevTracker.Application.Validators.WorkItems;

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.WorkItemId)
            .NotEmpty().WithMessage("O work item é obrigatório.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("O comentário não pode estar vazio.")
            .MaximumLength(4096).WithMessage("O comentário não pode ter mais de 4096 caracteres.");
    }
}
```

### CreateUserRequestValidator

```csharp
// src/DevTracker.Application/Validators/Users/CreateUserRequestValidator.cs
using DevTracker.Application.DTOs.Users;
using DevTracker.Core.Enums;
using FluentValidation;

namespace DevTracker.Application.Validators.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("O username é obrigatório.")
            .MinimumLength(3).WithMessage("O username deve ter pelo menos 3 caracteres.")
            .MaximumLength(64).WithMessage("O username não pode ter mais de 64 caracteres.")
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("O username só pode conter letras, números, '.', '_' e '-'.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A password é obrigatória.")
            .MinimumLength(12).WithMessage("A password deve ter pelo menos 12 caracteres.")
            .Matches(@"[A-Z]").WithMessage("A password deve conter pelo menos uma maiúscula.")
            .Matches(@"[a-z]").WithMessage("A password deve conter pelo menos uma minúscula.")
            .Matches(@"\d").WithMessage("A password deve conter pelo menos um número.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("A password deve conter pelo menos um símbolo.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("As passwords não coincidem.");

        RuleFor(x => x.Role)
            .NotEqual(UserRole.Owner).WithMessage("Não é permitido criar utilizadores com role Owner por este método.");
    }
}
```

---

## 5. Implementação dos Serviços

### ProjectService

```csharp
// src/DevTracker.Infrastructure/Services/ProjectService.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Security;
using DevTracker.Application.Validators.Projects;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

public sealed class ProjectService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService,
    IAuditService auditService) : IProjectService
{
    private static readonly CreateProjectRequestValidator CreateValidator = new();
    private static readonly ChangeProjectStateRequestValidator StateValidator = new();

    public async Task<Result<IReadOnlyList<ProjectSummaryDto>>> GetAllAsync(CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result<IReadOnlyList<ProjectSummaryDto>>.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectRead, ct);

        var projects = await db.Projects
            .AsNoTracking()
            .Select(p => new ProjectSummaryDto(
                p.Id,
                p.Name,
                p.Description,
                p.Color,
                p.State,
                p.WorkItems.Count,
                p.Repositories.Count,
                p.CreatedAt,
                p.UpdatedAt))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProjectSummaryDto>>.Success(projects);
    }

    public async Task<Result<ProjectDetailDto>> GetByIdAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result<ProjectDetailDto>.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectRead, ct);

        var project = await db.Projects
            .AsNoTracking()
            .Include(p => p.Repositories.Where(r => !r.IsDeleted))
            .Include(p => p.WorkItems.Where(w => !w.IsDeleted))
                .ThenInclude(w => w.AssignedToUser)
            .SingleOrDefaultAsync(p => p.Id == projectId, ct);

        if (project is null)
            return Result<ProjectDetailDto>.Fail("Projeto não encontrado.");

        var dto = new ProjectDetailDto(
            project.Id,
            project.Name,
            project.Description,
            project.Color,
            project.WorkspacePath,
            project.State,
            project.Repositories.Select(r => new RepositorySummaryDto(
                r.Id, r.Name, r.RelativePath, r.Description, r.CreatedAt)).ToList(),
            project.WorkItems.Select(w => new WorkItemSummaryDto(
                w.Id, w.Number, w.Title, w.Type, w.Status, w.Priority,
                w.AssignedToUser?.Username, w.DueDate, w.UpdatedAt)).ToList(),
            project.CreatedAt,
            project.UpdatedAt);

        return Result<ProjectDetailDto>.Success(dto);
    }

    public async Task<Result<Guid>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result<Guid>.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectCreate, ct);

        var validation = await CreateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<Guid>.Fail(validation.Errors[0].ErrorMessage);

        var duplicate = await db.Projects.AnyAsync(p => p.Name == request.Name, ct);
        if (duplicate)
            return Result<Guid>.Fail("Já existe um projeto com esse nome.");

        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Color = request.Color,
            WorkspacePath = request.WorkspacePath,
            State = ProjectState.NotStarted
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(
            currentUser.UserId.Value,
            AuditAction.ProjectCreated,
            nameof(Project),
            project.Id,
            null,
            $"{{\"name\":\"{project.Name}\"}}",
            ct);

        return Result<Guid>.Success(project.Id);
    }

    public async Task<Result> ChangeStateAsync(ChangeProjectStateRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectChangeState, ct);

        var validation = await StateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result.Fail(validation.Errors[0].ErrorMessage);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == request.ProjectId, ct);
        if (project is null)
            return Result.Fail("Projeto não encontrado.");

        var fromState = project.State;
        project.State = request.NewState;

        db.StateTransitions.Add(new StateTransition
        {
            ProjectId = project.Id,
            FromState = fromState,
            ToState = request.NewState,
            Reason = request.Reason,
            ChangedByUserId = currentUser.UserId.Value,
            ChangedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(
            currentUser.UserId.Value,
            AuditAction.ProjectStateChanged,
            nameof(Project),
            project.Id,
            fromState.ToString(),
            request.NewState.ToString(),
            ct);

        return Result.Success();
    }

    public async Task<Result> UpdateAsync(UpdateProjectRequest request, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectUpdate, ct);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == request.Id, ct);
        if (project is null)
            return Result.Fail("Projeto não encontrado.");

        var oldName = project.Name;
        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.Color = request.Color;

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(
            currentUser.UserId.Value,
            AuditAction.ProjectUpdated,
            nameof(Project),
            project.Id,
            $"{{\"name\":\"{oldName}\"}}",
            $"{{\"name\":\"{project.Name}\"}}",
            ct);

        return Result.Success();
    }

    public async Task<Result> ArchiveAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectArchive, ct);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
            return Result.Fail("Projeto não encontrado.");

        project.State = ProjectState.Cancelled;
        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(
            currentUser.UserId.Value,
            AuditAction.ProjectDeleted,
            nameof(Project),
            projectId,
            null,
            "Archived",
            ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!currentUser.UserId.HasValue)
            return Result.Fail("Utilizador não autenticado.");

        await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.ProjectDelete, ct);

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
            return Result.Fail("Projeto não encontrado.");

        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await auditService.LogAsync(
            currentUser.UserId.Value,
            AuditAction.ProjectDeleted,
            nameof(Project),
            projectId,
            null,
            "Deleted",
            ct);

        return Result.Success();
    }
}
```

### WorkItemService — geração do número sequencial

```csharp
// src/DevTracker.Infrastructure/Services/WorkItemService.cs (trecho CreateAsync)
public async Task<Result<Guid>> CreateAsync(CreateWorkItemRequest request, CancellationToken ct = default)
{
    if (!currentUser.UserId.HasValue)
        return Result<Guid>.Fail("Utilizador não autenticado.");

    await permissionService.EnsureCanPerformAsync(currentUser.UserId.Value, Permissions.WorkItemCreate, ct);

    var validation = await _createValidator.ValidateAsync(request, ct);
    if (!validation.IsValid)
        return Result<Guid>.Fail(validation.Errors[0].ErrorMessage);

    var projectExists = await db.Projects.AnyAsync(p => p.Id == request.ProjectId, ct);
    if (!projectExists)
        return Result<Guid>.Fail("Projeto não encontrado.");

    // Número sequencial por projeto — gerado no serviço, não pelo EF/SQLite
    var nextNumber = await db.WorkItems
        .IgnoreQueryFilters()
        .Where(w => w.ProjectId == request.ProjectId)
        .MaxAsync(w => (int?)w.Number, ct) ?? 0;

    nextNumber += 1;

    var workItem = new WorkItem
    {
        ProjectId = request.ProjectId,
        Number = nextNumber,
        Title = request.Title.Trim(),
        Description = request.Description?.Trim(),
        Type = request.Type,
        Priority = request.Priority,
        Status = WorkItemStatus.Backlog,
        CreatedByUserId = currentUser.UserId.Value,
        AssignedToUserId = request.AssignedToUserId,
        DueDate = request.DueDate
    };

    db.WorkItems.Add(workItem);
    await db.SaveChangesAsync(ct);

    await auditService.LogAsync(
        currentUser.UserId.Value,
        AuditAction.WorkItemCreated,
        nameof(WorkItem),
        workItem.Id,
        null,
        $"{{\"number\":{workItem.Number},\"title\":\"{workItem.Title}\"}}",
        ct);

    return Result<Guid>.Success(workItem.Id);
}
```

### AuditService

```csharp
// src/DevTracker.Infrastructure/Services/AuditService.cs
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Audit;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Infrastructure.Services;

public sealed class AuditService(AppDbContext db) : IAuditService
{
    public async Task LogAsync(
        Guid? userId,
        AuditAction action,
        string entityType,
        Guid? entityId,
        string? oldValue,
        string? newValue,
        CancellationToken ct = default)
    {
        var entry = new AuditEntry
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = DateTime.UtcNow,
            MachineName = Environment.MachineName
        };

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> GetRecentAsync(
        int count = 100,
        CancellationToken ct = default)
    {
        var entries = await db.AuditEntries
            .AsNoTracking()
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .Select(a => new AuditEntryDto(
                a.Id,
                a.User != null ? a.User.Username : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.OldValue,
                a.NewValue,
                a.Timestamp,
                a.MachineName))
            .ToListAsync(ct);

        return Result<IReadOnlyList<AuditEntryDto>>.Success(entries);
    }

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> GetByEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        var entries = await db.AuditEntries
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditEntryDto(
                a.Id,
                a.User != null ? a.User.Username : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.OldValue,
                a.NewValue,
                a.Timestamp,
                a.MachineName))
            .ToListAsync(ct);

        return Result<IReadOnlyList<AuditEntryDto>>.Success(entries);
    }

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> GetByUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var entries = await db.AuditEntries
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new AuditEntryDto(
                a.Id,
                a.User != null ? a.User.Username : null,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.OldValue,
                a.NewValue,
                a.Timestamp,
                a.MachineName))
            .ToListAsync(ct);

        return Result<IReadOnlyList<AuditEntryDto>>.Success(entries);
    }
}
```

---

## 6. Registo de Serviços (DI)

```csharp
// src/DevTracker.Application/DependencyInjection.cs
using DevTracker.Application.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Serviços de aplicação registados na Infrastructure (onde a implementação vive)
        // Este método pode ser usado para registar serviços puros da camada Application
        return services;
    }
}
```

```csharp
// src/DevTracker.Infrastructure/DependencyInjection.cs (extensão parcial)
public static IServiceCollection AddDomainServices(this IServiceCollection services)
{
    services.AddScoped<IProjectService, ProjectService>();
    services.AddScoped<IRepositoryService, RepositoryService>();
    services.AddScoped<IWorkItemService, WorkItemService>();
    services.AddScoped<IUserService, UserService>();
    services.AddScoped<IAuditService, AuditService>();
    services.AddScoped<ILabelService, LabelService>();

    return services;
}
```

---

## 7. Regras de negócio importantes

### WorkItem — número sequencial por projeto

- O `Number` é gerado como `MAX(Number) + 1` por projeto, usando `.IgnoreQueryFilters()` para contar também os soft-deleted.
- Nunca usar `AUTOINCREMENT` do SQLite para este campo — não seria por projeto.
- Em concorrência muito alta poderia haver race condition; dado que a app é desktop single-user, esta abordagem é segura.

### StateTransition — razão obrigatória

- `Reason` é obrigatório quando `NewState` é `Paused` ou `Cancelled`.
- Validação feita via `FluentValidation` antes de qualquer persistência.
- A entidade `StateTransition` é sempre INSERT, nunca UPDATE/DELETE.

### User — proteção do último Owner

- `DeleteAsync` e `DeactivateAsync` no `UserService` devem verificar se é o último Owner ativo.
- `ChangeRoleAsync` para remoção do role Owner deve ter a mesma proteção.

```csharp
if (user.Role == UserRole.Owner)
{
    var activeOwners = await db.Users
        .CountAsync(u => u.Role == UserRole.Owner && u.IsActive && u.Id != userId, ct);
    if (activeOwners == 0)
        return Result.Fail("Não é possível remover ou desativar o último Owner do sistema.");
}
```

### Audit — imutabilidade

- `AuditService.LogAsync` só faz INSERT.
- Nunca expor método de UPDATE ou DELETE na interface `IAuditService`.
- O `AuditEntryConfiguration` não deve ter `DeleteBehavior.Cascade` na relação com `User`.

### Soft delete

- `WorkItem`, `Project`, `Repository` e `Comment` têm soft delete.
- O EF Core aplica `QueryFilter` automaticamente: `.Where(x => !x.IsDeleted)`.
- Para operações de auditoria ou restauro, usar `.IgnoreQueryFilters()`.

---

## 8. Regras finais

1. Serviços nunca lançam exceções para erros de negócio — retornam `Result` ou `Result<T>`.
2. `AuthorizationException` é a única exceção permitida e só é lançada por `EnsureCanPerformAsync`.
3. Validação com FluentValidation acontece antes de qualquer escrita na BD.
4. `WorkItem.Number` é gerado no serviço, não pelo ORM.
5. `AuditService` só faz INSERT — nunca UPDATE/DELETE.
6. O último Owner nunca pode ser removido ou desativado.
7. `StateTransition` é imutável — só INSERT.
8. Operações destrutivas (delete físico, archive) requerem re-autenticação prévia coordenada pela UI (ver WORKSPACE.md e AUTH.md).
