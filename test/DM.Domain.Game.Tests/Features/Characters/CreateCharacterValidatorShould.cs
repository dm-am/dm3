using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CreateCharacterValidatorShould : UnitTestBase
{
    private readonly CreateCharacterValidator validator;
    private readonly Mock<ICharacterRepository> characterRepository;
    private readonly Mock<IAttributeValueValidator> attributeValueValidator;

    public CreateCharacterValidatorShould()
    {
        // The mocks, not just their objects: Mock<T>() hands out a new mock on
        // every call, so a setup written through a second call configured an
        // instance the validator never saw.
        characterRepository = Mock<ICharacterRepository>();
        attributeValueValidator = Mock<IAttributeValueValidator>();
        validator = new CreateCharacterValidator(characterRepository.Object, attributeValueValidator.Object);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        characterRepository
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
            Name = new string('a', CharacterPolicy.NameMaxLength + 1)
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task PassWithMaxLengthValues()
    {
        characterRepository
            .Setup(r => r.GameRequiresAttributes(It.IsAny<Guid>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

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
    /// single row per pair. This used to answer 500 rather than 400 — the
    /// required-attributes rule indexed the submitted values by identifier and
    /// threw on the second one.
    /// </summary>
    [Fact]
    public async Task FailWhenOneSpecificationIsSubmittedTwice()
    {
        var specificationId = Guid.NewGuid();
        characterRepository
            .Setup(r => r.GameRequiresAttributes(It.IsAny<Guid>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(true);
        characterRepository
            .Setup(r => r.GetGameSchema(It.IsAny<Guid>()))
            .ReturnsAsync(new AttributeSchema
            {
                Id = Guid.NewGuid(),
                Specifications =
                [
                    new AttributeSpecification
                    {
                        Id = specificationId, Title = "Race", Type = AttributeSpecificationType.Text
                    }
                ]
            });
        attributeValueValidator
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<AttributeSpecification>()))
            .Returns((true, (string?)null));

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
    /// A game without a schema skips every schema-driven rule, so this is the
    /// path on which the repeated identifier reached storage as two rows and
    /// left the character uneditable.
    /// </summary>
    [Fact]
    public async Task FailWhenOneSpecificationIsSubmittedTwiceToASchemalessGame()
    {
        var specificationId = Guid.NewGuid();
        characterRepository
            .Setup(r => r.GameRequiresAttributes(It.IsAny<Guid>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

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
