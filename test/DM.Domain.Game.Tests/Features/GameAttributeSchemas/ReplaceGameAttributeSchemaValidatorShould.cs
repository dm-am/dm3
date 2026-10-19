using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.GameAttributeSchemas;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.GameAttributeSchemas;

/// <summary>
/// What a descriptor of a game's sheet may be.
/// </summary>
public class ReplaceGameAttributeSchemaValidatorShould
{
    private readonly ReplaceGameAttributeSchemaValidator validator = new();

    private static ReplaceGameAttributeSchema Sheet(UpdateAttributeSpecification descriptor) => new()
    {
        GameId = Guid.NewGuid(),
        Version = 0,
        Specifications = [descriptor]
    };

    private static UpdateAttributeSpecification Descriptor(
        AttributeSpecificationType type = AttributeSpecificationType.Text, bool isHidden = false) =>
        new() { Title = "Класс", Type = type, IsDescriptor = true, IsHidden = isHidden };

    [Fact]
    public void PassAShownDescriptor() =>
        validator.TestValidate(Sheet(Descriptor())).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void RefuseAHiddenDescriptor() =>
        validator.TestValidate(Sheet(Descriptor(isHidden: true)))
            .ShouldHaveValidationErrorFor(s => s.Specifications)
            .WithErrorMessage("Дескриптор 'Класс' не может быть скрытым");

    [Fact]
    public void RefuseAMarkupDescriptor() =>
        validator.TestValidate(Sheet(Descriptor(AttributeSpecificationType.BbCode)))
            .ShouldHaveValidationErrorFor(s => s.Specifications)
            .WithErrorMessage("Атрибут 'Класс' с разметкой не может быть дескриптором");
}
