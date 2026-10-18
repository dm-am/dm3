using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemaTemplates;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.AttributeSchemaTemplates;

/// <summary>
/// A template is checked by the same specification rules as the sheet of a
/// game, and by the same limit on a title.
/// </summary>
public class AttributeSchemaTemplateValidatorsShould : UnitTestBase
{
    private readonly CreateAttributeSchemaTemplateValidator _createValidator = new();
    private readonly ReplaceAttributeSchemaTemplateValidator _replaceValidator = new();

    /// <summary>The longest title the site accepts, and one letter past it.</summary>
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
        _createValidator.TestValidate(Creation("Лайт", ValidSpecification()))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void PassAValidReplacement() =>
        _replaceValidator.TestValidate(Replacement("Лайт", ValidSpecification()))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void RefuseACreationWithoutATitle() =>
        _createValidator.TestValidate(Creation(""))
            .ShouldHaveValidationErrorFor(t => t.Title)
            .WithErrorMessage(ValidationError.Empty);

    [Fact]
    public void RefuseAReplacementWithoutATitle() =>
        _replaceValidator.TestValidate(Replacement(""))
            .ShouldHaveValidationErrorFor(t => t.Title)
            .WithErrorMessage(ValidationError.Empty);

    [Fact]
    public void RefuseACreationWithATooLongTitle() =>
        _createValidator.TestValidate(Creation(new string('a', LongestTitle + 1)))
            .ShouldHaveValidationErrorFor(t => t.Title)
            .WithErrorMessage(ValidationError.Long);

    [Fact]
    public void RefuseAReplacementWithATooLongTitle() =>
        _replaceValidator.TestValidate(Replacement(new string('a', LongestTitle + 1)))
            .ShouldHaveValidationErrorFor(t => t.Title)
            .WithErrorMessage(ValidationError.Long);

    /// <summary>
    /// A replacement addresses a template, and a body naming none is refused
    /// before anything is looked up.
    /// </summary>
    [Fact]
    public void RefuseAReplacementNamingNoTemplate() =>
        _replaceValidator.TestValidate(new ReplaceAttributeSchemaTemplate { Title = "Лайт" })
            .ShouldHaveValidationErrorFor(t => t.TemplateId)
            .WithErrorMessage(ValidationError.Empty);

    [Fact]
    public void RefuseASpecificationWithoutATitle()
    {
        var specification = ValidSpecification();
        specification.Title = "";

        _createValidator.TestValidate(Creation("Лайт", specification))
            .ShouldHaveValidationErrorFor("Specifications[0].Title")
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void RefuseASpecificationWithANegativeOrder()
    {
        var specification = ValidSpecification();
        specification.Order = -1;

        _createValidator.TestValidate(Creation("Лайт", specification))
            .ShouldHaveValidationErrorFor("Specifications[0].Order")
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void RefuseASecondDescriptor()
    {
        var first = ValidSpecification();
        first.IsDescriptor = true;
        var second = ValidSpecification();
        second.Title = "Agility";
        second.IsDescriptor = true;

        _createValidator.TestValidate(Creation("Лайт", first, second))
            .ShouldHaveValidationErrorFor(t => t.Specifications)
            .WithErrorMessage("Описателем можно отметить только один атрибут");
    }

    [Fact]
    public void RefuseAnEmptyListOfValues()
    {
        var specification = ValidSpecification();
        specification.Type = AttributeSpecificationType.TextList;
        specification.Values = [];

        _replaceValidator.TestValidate(Replacement("Лайт", specification))
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

        _replaceValidator.TestValidate(Replacement("Лайт", specification))
            .ShouldHaveValidationErrorFor(t => t.Specifications)
            .WithErrorMessage("В списке 'Strength' значения не должны повторяться");
    }
}
