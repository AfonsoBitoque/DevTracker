using Bogus;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;

namespace DevTracker.Tests.Helpers;

/// <summary>
/// Builder de utilizadores com Bogus para testes.
/// Padrão: utilizador ativo com Role=Reader.
/// </summary>
public sealed class UserBuilder
{
    private UserRole _role     = UserRole.Reader;
    private bool     _isActive = true;

    public UserBuilder WithRole(UserRole role) { _role = role; return this; }
    public UserBuilder Inactive()              { _isActive = false; return this; }

    public User Build()
    {
        var faker = new Faker<User>()
            .RuleFor(u => u.Id,           _ => Guid.NewGuid())
            .RuleFor(u => u.Username,     f => f.Internet.UserName().Replace(".", "_").ToLower())
            .RuleFor(u => u.PasswordHash, _ => Convert.ToBase64String(new byte[32]))
            .RuleFor(u => u.Salt,         _ => Convert.ToBase64String(new byte[32]))
            .RuleFor(u => u.Role,         _ => _role)
            .RuleFor(u => u.IsActive,     _ => _isActive)
            .RuleFor(u => u.CreatedAt,    f => f.Date.Past().ToUniversalTime())
            .RuleFor(u => u.UpdatedAt,    f => f.Date.Recent().ToUniversalTime());

        return faker.Generate();
    }

    public List<User> Build(int count)
        => Enumerable.Range(0, count).Select(_ => Build()).ToList();
}
