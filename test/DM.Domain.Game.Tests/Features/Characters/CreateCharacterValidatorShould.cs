using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CreateCharacterValidatorShould : UnitTestBase
{
    private readonly CreateCharacterValidator validator;
    private readonly ICharacterRepository characterRepository;
    private readonly IAttributeValueValidator attributeValueValidator;

    public CreateCharacterValidatorShould()
    {
        characterRepository = Mock<ICharacterRepository>();
        attributeValueValidator = Mock<IAttributeValueValidator>();
        // Every game carries a sheet, so every request reaches the read of one;
        // the tests that care about its content re-stub it.
        characterRepository.GetGameSchema(Arg.Any<Guid>()).Returns(new GameAttributeSchema());
        validator = new CreateCharacterValidator(characterRepository, attributeValueValidator);
    }

    [Fact]
    public async Task PassForValidInput()
    {
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
            Name = new string('a', CharacterPolicy.NameMaxLength + 1)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task PassWithMaxLengthValues()
    {
        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = new string('a', CharacterPolicy.NameMaxLength)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Two values for one specification cannot be stored: the character holds a
    /// single row per pair.
    /// </summary>
    [Fact]
    public async Task FailWhenOneSpecificationIsSubmittedTwice()
    {
        var specificationId = Guid.NewGuid();
        characterRepository
            .GetGameSchema(Arg.Any<Guid>()).Returns(new GameAttributeSchema
            {
                Specifications =
                [
                    new AttributeSpecification
                    {
                        Id = specificationId, Title = "Race", Type = AttributeSpecificationType.Text
                    }
                ]
            });
        attributeValueValidator
            .Validate(Arg.Any<string>(), Arg.Any<AttributeSpecification>()).Returns((true, (string?)null));

        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = "Character",
            Attributes =
            [
                new CharacterAttribute { Id = specificationId, Value = "Elf" },
                new CharacterAttribute { Id = specificationId, Value = "Dwarf" }
            ]
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(c => c.Attributes)
            .WithErrorMessage(AttributeValidationError.ManyDuplicated([specificationId]));
    }

    /// <summary>
    /// A game whose sheet holds no specification answers every schema-driven
    /// rule with nothing, so this is the path on which the repeated identifier
    /// reached storage as two rows and left the character uneditable.
    /// </summary>
    [Fact]
    public async Task FailWhenOneSpecificationIsSubmittedTwiceToAGameWithAnEmptySheet()
    {
        var specificationId = Guid.NewGuid();
        characterRepository
            .GetGameSchema(Arg.Any<Guid>()).Returns(new GameAttributeSchema());

        var input = new CreateCharacter
        {
            GameId = Guid.NewGuid(),
            Name = "Character",
            Attributes =
            [
                new CharacterAttribute { Id = specificationId, Value = "Elf" },
                new CharacterAttribute { Id = specificationId, Value = "Dwarf" }
            ]
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(c => c.Attributes)
            .WithErrorMessage(AttributeValidationError.ManyDuplicated([specificationId]));
    }
}
