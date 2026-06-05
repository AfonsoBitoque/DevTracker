# DevTracker — Testing Context

> Ficheiro de contexto: estratégia de testes, convenções, infraestrutura de testes e exemplos por camada.
> Usar em conjunto com STACK.md, DOMAIN.md, SERVICES.md e PERMISSIONS.md.

---

## 1. Estratégia de testes

### O que testar

| Camada | Testar | Não testar |
|---|---|---|
| `DevTracker.Core` | Value objects, validações de domain | Entidades simples sem lógica |
| `DevTracker.Application` | Validadores FluentValidation, lógica de permissões | DTOs sem lógica |
| `DevTracker.Infrastructure` | Serviços de aplicação (integração com SQLite in-memory), `PermissionService`, `AuthService` | Implementações EF triviais |
| `DevTracker.Desktop` | ViewModels críticos (ciclo de vida, comandos) | Views AXAML |

### O que NÃO testar (explicitamente)

- Entidades EF sem lógica própria
- DTOs e records sem transformação
- Configurações EF (`IEntityTypeConfiguration`) — cobertos pelos testes de integração
- Views AXAML — sem renderer em testes unitários

---

## 2. Stack de testes

```xml
<!-- tests/DevTracker.Application.Tests/DevTracker.Application.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="FluentAssertions" Version="6.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.*" />
    <PackageReference Include="Bogus" Version="35.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\DevTracker.Infrastructure\DevTracker.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

| Package | Uso |
|---|---|
| **xUnit** | Framework de testes |
| **FluentAssertions** | Asserções legíveis |
| **NSubstitute** | Mocks/stubs de interfaces |
| **SQLite in-memory** | BD de teste real sem ficheiro em disco |
| **Bogus** | Geração de dados falsos realistas |

---

## 3. Convenções de naming

### Formato dos métodos de teste

```
MethodName_Scenario_ExpectedBehavior
```

Exemplos:
- `CreateAsync_WithValidRequest_ReturnsSuccessWithProjectId`
- `CreateAsync_WhenNameAlreadyExists_ReturnsFail`
- `CanPerformAsync_WhenUserIsReader_CannotCreateProject`
- `LoginAsync_WithInvalidPassword_ReturnsAuthResultFail`

### Estrutura interna — padrão Arrange/Act/Assert

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    // ...

    // Act
    var result = await sut.MethodAsync(request);

    // Assert
    result.Succeeded.Should().BeTrue();
    result.Value.Should().NotBeEmpty();
}
```

---

## 4. Infraestrutura de testes — SQLite in-memory

### Factory de DbContext para testes

```csharp
// tests/DevTracker.Application.Tests/Infrastructure/TestDbContextFactory.cs
using DevTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevTracker.Application.Tests.Infrastructure;

public static class TestDbContextFactory
{
    /// <summary>
    /// Cria um AppDbContext com SQLite in-memory.
    /// Cada chamada cria uma BD isolada e migrada.
    /// </summary>
    public static AppDbContext Create(string? dbName = null)
    {
        var name = dbName ?? Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={name};Mode=Memory;Cache=Shared")
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}
```

### Nota: por que SQLite in-memory e não EF InMemory?

- `EF InMemory` não suporta transações, constraints de FK, índices únicos, nem `HasQueryFilter`.
- `SQLite in-memory` é uma BD real com schema completo — garante que os testes cobrem comportamentos reais do ORM.

---

## 5. Fixtures e helpers de dados

### UserBuilder (Bogus)

```csharp
// tests/DevTracker.Application.Tests/Builders/UserBuilder.cs
using Bogus;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;

namespace DevTracker.Application.Tests.Builders;

public sealed class UserBuilder
{
    private readonly Faker<User> _faker = new Faker<User>("pt_PT")
        .RuleFor(u => u.Id, f => Guid.NewGuid())
        .RuleFor(u => u.Username, f => f.Internet.UserName())
        .RuleFor(u => u.PasswordHash, _ => "hashed_password")
        .RuleFor(u => u.Salt, _ => "random_salt")
        .RuleFor(u => u.Role, _ => UserRole.Maintainer)
        .RuleFor(u => u.IsActive, _ => true)
        .RuleFor(u => u.CreatedAt, f => f.Date.Past())
        .RuleFor(u => u.UpdatedAt, f => f.Date.Recent());

    public UserBuilder WithRole(UserRole role)
    {
        _faker.RuleFor(u => u.Role, _ => role);
        return this;
    }

    public UserBuilder Inactive()
    {
        _faker.RuleFor(u => u.IsActive, _ => false);
        return this;
    }

    public User Build() => _faker.Generate();

    public List<User> Build(int count) => _faker.Generate(count);
}
```

### ProjectBuilder

```csharp
// tests/DevTracker.Application.Tests/Builders/ProjectBuilder.cs
using Bogus;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;

namespace DevTracker.Application.Tests.Builders;

public sealed class ProjectBuilder
{
    private readonly Faker<Project> _faker = new Faker<Project>("pt_PT")
        .RuleFor(p => p.Id, _ => Guid.NewGuid())
        .RuleFor(p => p.Name, f => f.Commerce.ProductName())
        .RuleFor(p => p.Color, _ => "#4f98a3")
        .RuleFor(p => p.WorkspacePath, f => f.System.DirectoryPath())
        .RuleFor(p => p.State, _ => ProjectState.NotStarted)
        .RuleFor(p => p.IsDeleted, _ => false)
        .RuleFor(p => p.CreatedAt, f => f.Date.Past())
        .RuleFor(p => p.UpdatedAt, f => f.Date.Recent());

    public ProjectBuilder WithState(ProjectState state)
    {
        _faker.RuleFor(p => p.State, _ => state);
        return this;
    }

    public Project Build() => _faker.Generate();
}
```

---

## 6. Testes — PermissionService

```csharp
// tests/DevTracker.Application.Tests/Security/PermissionServiceTests.cs
using DevTracker.Application.Security;
using DevTracker.Application.Tests.Builders;
using DevTracker.Application.Tests.Infrastructure;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Security;
using FluentAssertions;

namespace DevTracker.Application.Tests.Security;

public sealed class PermissionServiceTests
{
    [Theory]
    [InlineData(UserRole.Owner, Permissions.ProjectDelete, true)]
    [InlineData(UserRole.Admin, Permissions.ProjectDelete, false)]
    [InlineData(UserRole.Maintainer, Permissions.ProjectDelete, false)]
    [InlineData(UserRole.Reader, Permissions.ProjectDelete, false)]
    [InlineData(UserRole.Owner, Permissions.ProjectCreate, true)]
    [InlineData(UserRole.Admin, Permissions.ProjectCreate, true)]
    [InlineData(UserRole.Maintainer, Permissions.ProjectCreate, false)]
    [InlineData(UserRole.Reader, Permissions.ProjectCreate, false)]
    [InlineData(UserRole.Reader, Permissions.WorkItemComment, true)]
    [InlineData(UserRole.Reader, Permissions.WorkItemCreate, false)]
    public async Task CanPerformAsync_ReturnsCorrectPermission_ForRole(
        UserRole role, string permission, bool expected)
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = new UserBuilder().WithRole(role).Build();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = new PermissionService(db);

        // Act
        var result = await sut.CanPerformAsync(user.Id, permission);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task CanPerformAsync_WhenUserIsInactive_ReturnsFalse()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = new UserBuilder().WithRole(UserRole.Owner).Inactive().Build();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = new PermissionService(db);

        // Act
        var result = await sut.CanPerformAsync(user.Id, Permissions.ProjectCreate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanPerformAsync_WhenUserDoesNotExist_ReturnsFalse()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var sut = new PermissionService(db);

        // Act
        var result = await sut.CanPerformAsync(Guid.NewGuid(), Permissions.ProjectRead);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCanPerformAsync_WhenNotAuthorized_ThrowsAuthorizationException()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = new UserBuilder().WithRole(UserRole.Reader).Build();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sut = new PermissionService(db);

        // Act
        var act = async () => await sut.EnsureCanPerformAsync(user.Id, Permissions.ProjectDelete);

        // Assert
        await act.Should().ThrowAsync<AuthorizationException>();
    }
}
```

---

## 7. Testes — Validadores FluentValidation

```csharp
// tests/DevTracker.Application.Tests/Validators/CreateProjectRequestValidatorTests.cs
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Validators.Projects;
using FluentAssertions;

namespace DevTracker.Application.Tests.Validators;

public sealed class CreateProjectRequestValidatorTests
{
    private readonly CreateProjectRequestValidator _sut = new();

    [Fact]
    public async Task Validate_WithValidRequest_ShouldPass()
    {
        // Arrange
        var request = new CreateProjectRequest(
            Name: "Meu Projeto",
            Description: null,
            Color: "#4f98a3",
            WorkspacePath: "/home/user/workspace");

        // Act
        var result = await _sut.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public async Task Validate_WithInvalidName_ShouldFail(string name)
    {
        var request = new CreateProjectRequest(name, null, "#4f98a3", "/workspace");
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("#GGGGGG")]
    [InlineData("4f98a3")]
    [InlineData("#4f98")]
    [InlineData("")]
    public async Task Validate_WithInvalidColor_ShouldFail(string color)
    {
        var request = new CreateProjectRequest("Nome Válido", null, color, "/workspace");
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("project/bad", "O nome contém caracteres inválidos")]
    [InlineData("project\\bad", "O nome contém caracteres inválidos")]
    public async Task Validate_WithPathCharactersInName_ShouldFail(string name, string expectedError)
    {
        var request = new CreateProjectRequest(name, null, "#4f98a3", "/workspace");
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError));
    }
}
```

```csharp
// tests/DevTracker.Application.Tests/Validators/ChangeProjectStateRequestValidatorTests.cs
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Validators.Projects;
using DevTracker.Core.Enums;
using FluentAssertions;

namespace DevTracker.Application.Tests.Validators;

public sealed class ChangeProjectStateRequestValidatorTests
{
    private readonly ChangeProjectStateRequestValidator _sut = new();

    [Theory]
    [InlineData(ProjectState.Paused)]
    [InlineData(ProjectState.Cancelled)]
    public async Task Validate_WhenStateRequiresReason_AndReasonIsEmpty_ShouldFail(ProjectState state)
    {
        var request = new ChangeProjectStateRequest(Guid.NewGuid(), state, null);
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WhenStateRequiresReason_AndReasonIsProvided_ShouldPass()
    {
        var request = new ChangeProjectStateRequest(
            Guid.NewGuid(), ProjectState.Paused, "À espera de aprovação do cliente");
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectState.InProgress)]
    [InlineData(ProjectState.Completed)]
    [InlineData(ProjectState.NotStarted)]
    public async Task Validate_WhenStateDoesNotRequireReason_ShouldPass(ProjectState state)
    {
        var request = new ChangeProjectStateRequest(Guid.NewGuid(), state, null);
        var result = await _sut.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }
}
```

---

## 8. Testes — ProjectService (integração)

```csharp
// tests/DevTracker.Application.Tests/Services/ProjectServiceTests.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Security;
using DevTracker.Application.Tests.Builders;
using DevTracker.Application.Tests.Infrastructure;
using DevTracker.Core.Enums;
using DevTracker.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;

namespace DevTracker.Application.Tests.Services;

public sealed class ProjectServiceTests
{
    private static (IProjectService sut, AppDbContext db) CreateSut(
        UserRole actingRole = UserRole.Owner)
    {
        var db = TestDbContextFactory.Create();

        var user = new UserBuilder().WithRole(actingRole).Build();
        db.Users.Add(user);
        db.SaveChanges();

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(user.Id);
        currentUser.IsAuthenticated.Returns(true);

        var permissionService = new PermissionService(db);
        var auditService = Substitute.For<IAuditService>();

        var sut = new ProjectService(db, currentUser, permissionService, auditService);

        return (sut, db);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsSuccessWithProjectId()
    {
        // Arrange
        var (sut, _) = CreateSut(UserRole.Owner);
        var request = new CreateProjectRequest("Meu Projeto", null, "#4f98a3", "/workspace");

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenMaintainer_ReturnsFail()
    {
        // Arrange
        var (sut, _) = CreateSut(UserRole.Maintainer);
        var request = new CreateProjectRequest("Meu Projeto", null, "#4f98a3", "/workspace");

        // Act & Assert
        var act = async () => await sut.CreateAsync(request);
        await act.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task CreateAsync_WhenNameDuplicated_ReturnsFail()
    {
        // Arrange
        var (sut, db) = CreateSut(UserRole.Owner);
        var project = new ProjectBuilder().Build();
        project.Name = "Projeto Duplicado";
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var request = new CreateProjectRequest("Projeto Duplicado", null, "#4f98a3", "/workspace");

        // Act
        var result = await sut.CreateAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("nome");
    }

    [Fact]
    public async Task ChangeStateAsync_ToPaused_WithoutReason_ReturnsFail()
    {
        // Arrange
        var (sut, db) = CreateSut(UserRole.Owner);
        var project = new ProjectBuilder().WithState(ProjectState.InProgress).Build();
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var request = new ChangeProjectStateRequest(project.Id, ProjectState.Paused, null);

        // Act
        var result = await sut.ChangeStateAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeStateAsync_ToPaused_WithReason_CreatesStateTransition()
    {
        // Arrange
        var (sut, db) = CreateSut(UserRole.Owner);
        var project = new ProjectBuilder().WithState(ProjectState.InProgress).Build();
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var request = new ChangeProjectStateRequest(project.Id, ProjectState.Paused, "A aguardar aprovação");

        // Act
        var result = await sut.ChangeStateAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        db.StateTransitions.Should().ContainSingle(
            st => st.ProjectId == project.Id && st.ToState == ProjectState.Paused);
    }
}
```

---

## 9. Testes — AuthService

```csharp
// tests/DevTracker.Application.Tests/Security/AuthServiceTests.cs
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Tests.Infrastructure;
using DevTracker.Infrastructure.Security;
using FluentAssertions;
using NSubstitute;

namespace DevTracker.Application.Tests.Security;

public sealed class AuthServiceTests
{
    private static AuthService CreateSut(AppDbContext db)
    {
        var passwordHasher = new Argon2PasswordHasher();
        var sessionService = new InMemorySessionService();
        var auditService = Substitute.For<IAuditService>();

        return new AuthService(db, passwordHasher, sessionService, auditService);
    }

    [Fact]
    public async Task SetupFirstOwnerAsync_WhenNoUsersExist_CreatesOwnerAndReturnsSuccess()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        // Act
        var result = await sut.SetupFirstOwnerAsync("admin", "Passw0rd!Secure");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Session.Should().NotBeNull();
        db.Users.Should().ContainSingle();
    }

    [Fact]
    public async Task SetupFirstOwnerAsync_WhenUsersAlreadyExist_ReturnsFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);
        await sut.SetupFirstOwnerAsync("admin", "Passw0rd!Secure");

        // Act — segunda tentativa
        var result = await sut.SetupFirstOwnerAsync("outro", "Passw0rd!Secure");

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsSuccessWithSession()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);
        await sut.SetupFirstOwnerAsync("admin", "Passw0rd!Secure");

        // Act
        var result = await sut.LoginAsync("admin", "Passw0rd!Secure");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Session.Should().NotBeNull();
        result.Session!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);
        await sut.SetupFirstOwnerAsync("admin", "Passw0rd!Secure");

        // Act
        var result = await sut.LoginAsync("admin", "WrongPassword!");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Credenciais");
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ReturnsFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        // Act
        var result = await sut.LoginAsync("naoexiste", "Passw0rd!Secure");

        // Assert
        result.Succeeded.Should().BeFalse();
    }
}
```

---

## 10. Testes — WorkItem número sequencial

```csharp
// tests/DevTracker.Application.Tests/Services/WorkItemServiceTests.cs (trecho)
[Fact]
public async Task CreateAsync_AssignsSequentialNumberPerProject()
{
    // Arrange
    var (sut, db) = CreateSut(UserRole.Owner);
    var project = new ProjectBuilder().Build();
    db.Projects.Add(project);
    await db.SaveChangesAsync();

    var request1 = new CreateWorkItemRequest(project.Id, "Task 1", null, WorkItemType.Task, WorkItemPriority.Medium, null, null);
    var request2 = new CreateWorkItemRequest(project.Id, "Task 2", null, WorkItemType.Task, WorkItemPriority.Medium, null, null);

    // Act
    var r1 = await sut.CreateAsync(request1);
    var r2 = await sut.CreateAsync(request2);

    // Assert
    r1.Succeeded.Should().BeTrue();
    r2.Succeeded.Should().BeTrue();

    var items = db.WorkItems.Where(w => w.ProjectId == project.Id).OrderBy(w => w.Number).ToList();
    items[0].Number.Should().Be(1);
    items[1].Number.Should().Be(2);
}
```

---

## 11. Comandos de execução de testes

```bash
# Todos os testes
dotnet test

# Testes com output detalhado
dotnet test --verbosity normal

# Testes com coverage (requer coverlet)
dotnet test --collect:"XPlat Code Coverage"

# Apenas testes de um projeto
dotnet test tests/DevTracker.Application.Tests/

# Filtrar por categoria
dotnet test --filter "FullyQualifiedName~PermissionService"
```

---

## 12. Regras finais

1. Usar SQLite in-memory nos testes de integração, nunca EF InMemory Provider.
2. Cada teste cria a sua própria instância de `AppDbContext` com BD isolada.
3. `NSubstitute` para mockar interfaces externas (serviços de IO, sessão, etc.).
4. `FluentAssertions` para todas as asserções — nunca `Assert.Equal` do xUnit diretamente.
5. Naming: `MethodName_Scenario_ExpectedBehavior`.
6. Padrão: Arrange / Act / Assert, com comentários obrigatórios.
7. Nunca mockar o `AppDbContext` — usar sempre a BD in-memory real.
8. Testes não devem depender de ordem de execução.
9. Dados de teste gerados com `Bogus` nos builders — nunca hardcoded em massa.
