using System;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CreateCharacterValidatorShould : UnitTestBase
{
    private readonly CreateCharacterValidator validator;
    private readonly ICharacterRepository characterRepository;
    private readonly IAttributeValueValidator attributeValueValidator;

    public CreateCharacterValidatorShould()
    {
        characterRepository = Mock<ICharacterRepository>().Object;
        attributeValueValidator = Mock<IAttributeValueValidator>().Object;
        validator = new CreateCharacterValidator(characterRepository, attributeValueValidator);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        Mock<ICharacterRepository>()
            .Setup(r => r.GameRequiresAttributes(It.IsAny<Guid>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = "Valid Character Name"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenNameIsEmpty()
    {
        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenNameIsNull()
    {
        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = null!
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task FailWhenNameExceedsMaxLength()
    {
        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = new string('a', 51)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task PassWithMaxLengthValues()
    {
        Mock<ICharacterRepository>()
            .Setup(r => r.GameRequiresAttributes(It.IsAny<Guid>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = new string('a', 50)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
