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

public class UpdateCharacterValidatorShould : UnitTestBase
{
    private readonly UpdateCharacterValidator validator;
    private readonly Mock<ICharacterRepository> characterRepository;
    private readonly Mock<IAttributeValueValidator> attributeValueValidator;

    public UpdateCharacterValidatorShould()
    {
        // The mocks, not just their objects: Mock<T>() hands out a new mock on
        // every call, so a setup written through a second call would configure
        // an instance the validator never saw.
        characterRepository = Mock<ICharacterRepository>();
        attributeValueValidator = Mock<IAttributeValueValidator>();
        validator = new UpdateCharacterValidator(characterRepository.Object, attributeValueValidator.Object);
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
            Name = new string('a', CharacterPolicy.NameMaxLength + 1)
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

    /// <summary>
    /// The same invariant as on create: the character holds one value per
    /// specification, so a repeated identifier is a caller's mistake and not a
    /// failed save.
    /// </summary>
    [Fact]
    public async Task FailWhenOneSpecificationIsSubmittedTwice()
    {
        var specificationId = Guid.NewGuid();
        characterRepository
            .Setup(r => r.GetCharacterSchema(It.IsAny<Guid>()))
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

        var input = new UpdateCharacter
        {
            CharacterId = Guid.NewGuid(),
            Name = "Updated Character Name",
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
