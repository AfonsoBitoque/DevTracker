using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Validators.Projects;
using FluentAssertions;

namespace DevTracker.Tests.Application.Validators;

public sealed class CreateProjectRequestValidatorTests
{
    private readonly CreateProjectRequestValidator _validator = new();

    private static CreateProjectRequest ValidRequest() =>
        new("Meu Projeto", null, "#2563EB", "/workspace/meu-projeto", null);

    [Fact]
    public async Task Validate_ValidRequest_ShouldPass()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("A")]           // muito curto
    [InlineData("")]            // vazio
    public async Task Validate_InvalidName_TooShort_ShouldFail(string name)
    {
        // Arrange
        var request = ValidRequest() with { Name = name };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Projeto/Invalido")]
    [InlineData("Projeto\\Invalido")]
    [InlineData("Projeto:Invalido")]
    [InlineData("Projeto*Invalido")]
    [InlineData("Projeto?Invalido")]
    public async Task Validate_NameWithPathChars_ShouldFail(string name)
    {
        // Arrange
        var request = ValidRequest() with { Name = name };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData("#ZZZZZZ")]     // hex inválido
    [InlineData("2563EB")]      // sem #
    [InlineData("#25")]         // muito curto
    [InlineData("")]            // vazio
    public async Task Validate_InvalidColor_ShouldFail(string color)
    {
        // Arrange
        var request = ValidRequest() with { Color = color };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Color");
    }
}
