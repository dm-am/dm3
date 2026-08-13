using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.AttributeSchemas;

public class UpdateAttributeSchemaValidatorShould : UnitTestBase
{
    private readonly UpdateAttributeSchemaValidator validator = new();

    private static UpdateAttributeSpecification ValidSpecification() => new()
    {
        Title = "Strength",
        Type = AttributeSpecificationType.Number,
        Order = 0
    };

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Title = "Valid Schema",
            Type = SchemaType.Public,
            Specifications = [ValidSpecification()]
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenOptionalFieldsAreOmitted()
    {
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid()
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenSchemaIdIsEmpty()
    {
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.Empty
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.SchemaId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Title = new string('a', 101)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenTypeIsNotInEnum()
    {
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Type = (SchemaType)99
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Type)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenSpecificationTitleIsEmpty()
    {
        var specification = ValidSpecification();
        specification.Title = "";
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [specification]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Specifications[0].Title")
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenSpecificationTypeIsNotInEnum()
    {
        var specification = ValidSpecification();
        specification.Type = (AttributeSpecificationType)99;
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [specification]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Specifications[0].Type")
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenSpecificationOrderIsNegative()
    {
        var specification = ValidSpecification();
        specification.Order = -1;
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [specification]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Specifications[0].Order")
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenMultipleDescriptorsArePresent()
    {
        var first = ValidSpecification();
        first.IsDescriptor = true;
        var second = ValidSpecification();
        second.Title = "Agility";
        second.IsDescriptor = true;
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [first, second]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Specifications)
            .WithErrorMessage("Описателем можно отметить только один атрибут");
    }

    [Fact]
    public void FailWhenBbCodeSpecificationIsDescriptor()
    {
        var specification = ValidSpecification();
        specification.Type = AttributeSpecificationType.BbCode;
        specification.IsDescriptor = true;
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [specification]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Specifications)
            .WithErrorMessage("Атрибут 'Strength' с разметкой не может быть описателем");
    }

    [Fact]
    public void FailWhenListSpecificationHasNoValues()
    {
        var specification = ValidSpecification();
        specification.Type = AttributeSpecificationType.NumberList;
        specification.Values = [];
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [specification]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Specifications)
            .WithErrorMessage("У списка 'Strength' должно быть хотя бы одно значение");
    }

    [Fact]
    public void FailWhenListSpecificationHasDuplicateValues()
    {
        var specification = ValidSpecification();
        specification.Type = AttributeSpecificationType.TextList;
        specification.Values =
        [
            new ListValue { Value = "Sword" },
            new ListValue { Value = "Sword" }
        ];
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [specification]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Specifications)
            .WithErrorMessage("В списке 'Strength' значения не должны повторяться");
    }

    [Fact]
    public void FailWhenTextNumberListValueHasNoModifier()
    {
        var specification = ValidSpecification();
        specification.Type = AttributeSpecificationType.TextNumberList;
        specification.Values = [new ListValue { Value = "Sword", Modifier = null }];
        var input = new UpdateAttributeSchema
        {
            SchemaId = Guid.NewGuid(),
            Specifications = [specification]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(s => s.Specifications)
            .WithErrorMessage("В списке 'Strength' у каждого варианта нужны и значение, и модификатор");
    }
}
