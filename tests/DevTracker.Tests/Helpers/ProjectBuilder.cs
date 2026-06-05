using Bogus;
using DevTracker.Core.Entities;
using DevTracker.Core.Enums;

namespace DevTracker.Tests.Helpers;

/// <summary>Builder de projetos com Bogus para testes.</summary>
public sealed class ProjectBuilder
{
    private ProjectState _state = ProjectState.NotStarted;

    public ProjectBuilder WithState(ProjectState state) { _state = state; return this; }

    public Project Build()
    {
        var faker = new Faker<Project>()
            .RuleFor(p => p.Id,            _ => Guid.NewGuid())
            .RuleFor(p => p.Name,          f => f.Commerce.ProductName())
            .RuleFor(p => p.Description,   f => f.Lorem.Sentence())
            .RuleFor(p => p.Color,         _ => "#2563EB")
            .RuleFor(p => p.WorkspacePath, f => $"/tmp/workspace/{f.Random.AlphaNumeric(8)}")
            .RuleFor(p => p.State,         _ => _state)
            .RuleFor(p => p.IsDeleted,     _ => false)
            .RuleFor(p => p.CreatedAt,     f => f.Date.Past().ToUniversalTime())
            .RuleFor(p => p.UpdatedAt,     f => f.Date.Recent().ToUniversalTime());

        return faker.Generate();
    }
}
