using System;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class UpdateCharacterValidatorShould : UnitTestBase
{
    private readonly UpdateCharacterValidator validator;
    private readonly ICharacterRepository characterRepository;
    private readonly IAttributeValueValidator attributeValueValidator;

    public UpdateCharacterValidatorShould()
    {
        characterRepository = Mock<ICharacterRepository>().Object;
        attributeValueValidator = Mock<IAttributeValueValidator>().Object;
        validator = new UpdateCharacterValidator(characterRepository, attributeValueValidator);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new UpdateCharacter
        {
            CharacterId = Guid.NewGuid(),
            Name = "Updated Character Name"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenNameIsEmptyString()
    {
        var input = new UpdateCharacter
        {
            CharacterId = Guid.NewGuid(),
            Name = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenNameExceedsMaxLength()
    {
        var input = new UpdateCharacter
        {
            CharacterId = Guid.NewGuid(),
            Name = new string('a', 51)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task PassWhenOptionalFieldsAreNull()
    {
        var input = new UpdateCharacter
        {
            CharacterId = Guid.NewGuid()
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
