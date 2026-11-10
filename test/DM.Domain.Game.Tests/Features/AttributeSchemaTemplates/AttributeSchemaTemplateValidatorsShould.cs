using System;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemaTemplates;
using DM.Domain.Game.Features.AttributeSchemas;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.AttributeSchemaTemplates;

/// <summary>
/// A template is checked by the same specification rules as the sheet of a
/// game, and by the same limit on a title.
/// </summary>
public class AttributeSchemaTemplateValidatorsShould
{
    private readonly CreateAttributeSchemaTemplateValidator createValidator = new();
    private readonly ReplaceAttributeSchemaTemplateValidator replaceValidator = new();

    private const int LongestTitle = 100;

    private static CreateAttributeSpecification ValidSpecification() => new()
    {
        Title = "Strength",
        Type = AttributeSpecificationType.Number,
        Order = 0
    };

    private static CreateAttributeSchemaTemplate Creation(
        string title = "Лайт", params CreateAttributeSpecification[] specifications) =>
        new() { Title = title, Specifications = specifications };

    private static ReplaceAttributeSchemaTemplate Replacement(
        string title = "Лайт", params CreateAttributeSpecification[] specifications) =>
        new() { TemplateId = Guid.NewGuid(), Title = title, Specifications = specifications };

    [Fact]
    public void PassAValidCreation() =>
        createValidator.TestValidate(Creation("Лайт", ValidSpecification()))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void PassAValidReplacement() =>
        replaceValidator.TestValidate(Replacement("Лайт", ValidSpecification()))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0, ValidationError.Empty)]
    [InlineData(LongestTitle + 1, ValidationError.Long)]
    public void RefuseAnEmptyOrTooLongTitleOnBothWrites(int length, string error)
    {
        var title = new string('a', length);

        createValidator.TestValidate(Creation(title))
            .ShouldHaveValidationErrorFor(t => t.Title).WithErrorMessage(error);
        replaceValidator.TestValidate(Replacement(title))
            .ShouldHaveValidationErrorFor(t => t.Title).WithErrorMessage(error);
    }

    // A replacement addresses a template, and a body naming none is refused before anything is
    // looked up.
    [Fact]
    public void RefuseAReplacementNamingNoTemplate() =>
        replaceValidator.TestValidate(new ReplaceAttributeSchemaTemplate { Title = "Лайт" })
            .ShouldHaveValidationErrorFor(t => t.TemplateId)
            .WithErrorMessage(ValidationError.Empty);

    // One refusal for one empty field: the schema-level rules name the attribute in their sentences
    // and do not repeat the field's own refusal.
    [Fact]
    public void RefuseASpecificationWithoutATitleOnce()
    {
        var specification = ValidSpecification();
        specification.Title = "";

        var result = createValidator.TestValidate(Creation("Лайт", specification));

        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Введите название атрибута");
        result.ShouldHaveValidationErrorFor("Specifications[0].Title");
    }

    [Fact]
    public void RefuseASpecificationWithANegativeOrder()
    {
        var specification = ValidSpecification();
        specification.Order = -1;

        createValidator.TestValidate(Creation("Лайт", specification))
            .ShouldHaveValidationErrorFor("Specifications[0].Order")
            .WithErrorMessage(ValidationError.Invalid);
    }

    // A maximal length below one admits no value at all: every character sheet written against such
    // a specification would be refused.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefuseASpecificationWithANonPositiveMaxLength(int maxLength)
    {
        var specification = ValidSpecification();
        specification.MaxLength = maxLength;

        createValidator.TestValidate(Creation("Лайт", specification))
            .ShouldHaveValidationErrorFor("Specifications[0].MaxLength")
            .WithErrorMessage(ValidationError.MustBePositive);
        replaceValidator.TestValidate(Replacement("Лайт", specification))
            .ShouldHaveValidationErrorFor("Specifications[0].MaxLength")
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void PassASpecificationWithTheShortestMaxLength()
    {
        var specification = ValidSpecification();
        specification.MaxLength = 1;

        createValidator.TestValidate(Creation("Лайт", specification))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefuseASecondDescriptor()
    {
        var first = ValidSpecification();
        first.IsDescriptor = true;
        var second = ValidSpecification();
        second.Title = "Agility";
        second.IsDescriptor = true;

        createValidator.TestValidate(Creation("Лайт", first, second))
            .ShouldHaveValidationErrorFor(t => t.Specifications)
            .WithErrorMessage("Дескриптором можно отметить только один атрибут");
    }

    [Fact]
    public void RefuseAnEmptyListOfValues()
    {
        var specification = ValidSpecification();
        specification.Type = AttributeSpecificationType.TextList;
        specification.Values = [];

        replaceValidator.TestValidate(Replacement("Лайт", specification))
            .ShouldHaveValidationErrorFor(t => t.Specifications)
            .WithErrorMessage("У списка 'Strength' должно быть хотя бы одно значение");
    }

    [Fact]
    public void RefuseAListWithRepeatedValues()
    {
        var specification = ValidSpecification();
        specification.Type = AttributeSpecificationType.TextList;
        specification.Values =
        [
            new ListValue { Value = "Sword" },
            new ListValue { Value = "Sword" }
        ];

        replaceValidator.TestValidate(Replacement("Лайт", specification))
            .ShouldHaveValidationErrorFor(t => t.Specifications)
            .WithErrorMessage("В списке 'Strength' значения не должны повторяться");
    }
}
