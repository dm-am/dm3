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

public class UpdateCharacterValidatorShould : UnitTestBase
{
    private readonly UpdateCharacterValidator validator;
    private readonly ICharacterRepository characterRepository;
    private readonly IAttributeValueValidator attributeValueValidator;

    public UpdateCharacterValidatorShould()
    {
        characterRepository = Mock<ICharacterRepository>();
        attributeValueValidator = Mock<IAttributeValueValidator>();
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

    // A sheet with no specifications in it accepts no value: the identifier submitted answers to
    // nothing, which is the same refusal an unknown one earns in a game whose sheet is full.
    [Fact]
    public async Task RefuseAValueWhenTheSheetHoldsNoSpecifications()
    {
        characterRepository
            .GetCharacterSchema(Arg.Any<Guid>()).Returns(new GameAttributeSchema());

        var input = new UpdateCharacter
        {
            CharacterId = Guid.NewGuid(),
            Name = "Updated Character Name",
            Attributes =
            [
                new CharacterAttribute { Id = Guid.NewGuid(), Value = "Elf" }
            ]
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor("Attributes[0]")
            .WithErrorMessage(AttributeValidationError.InvalidSpecification);
    }

    // The same invariant as on create: the character holds one value per specification, so a
    // repeated identifier is a caller's mistake and not a failed save.
    [Fact]
    public async Task FailWhenOneSpecificationIsSubmittedTwice()
    {
        var specificationId = Guid.NewGuid();
        characterRepository
            .GetCharacterSchema(Arg.Any<Guid>()).Returns(new GameAttributeSchema
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
