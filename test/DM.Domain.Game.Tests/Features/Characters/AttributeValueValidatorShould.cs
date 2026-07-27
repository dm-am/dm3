using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class AttributeValueValidatorShould : UnitTestBase
{
    private readonly AttributeValueValidator validator = new();

    private static AttributeSpecification Specification(
        AttributeSpecificationType type,
        bool required = false,
        int? maxLength = null,
        params string[] listValues) => new()
    {
        Title = "Attribute",
        Type = type,
        Required = required,
        MaxLength = maxLength,
        Values = System.Array.ConvertAll(listValues, v => new ListValue { Value = v })
    };

    [Fact]
    public void FailWhenValueIsNull()
    {
        var (valid, error) = validator.Validate(null!, Specification(AttributeSpecificationType.Text));

        valid.Should().BeFalse();
        error.Should().Be(ValidationError.Empty);
    }

    [Theory]
    [InlineData(AttributeSpecificationType.Text)]
    [InlineData(AttributeSpecificationType.Number)]
    [InlineData(AttributeSpecificationType.TextList)]
    [InlineData(AttributeSpecificationType.NumberList)]
    [InlineData(AttributeSpecificationType.TextNumberList)]
    [InlineData(AttributeSpecificationType.BbCode)]
    public void FailWhenRequiredValueIsEmpty(AttributeSpecificationType type)
    {
        var (valid, error) = validator.Validate("   ", Specification(type, required: true, listValues: "Option"));

        valid.Should().BeFalse();
        error.Should().Be(AttributeValidationError.RequiredMissing);
    }

    [Theory]
    [InlineData(AttributeSpecificationType.Text)]
    [InlineData(AttributeSpecificationType.BbCode)]
    public void PassWhenOptionalTextValueIsEmpty(AttributeSpecificationType type)
    {
        var (valid, error) = validator.Validate("", Specification(type, maxLength: 10));

        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("42")]
    [InlineData("-17")]
    [InlineData(" 5 ")]
    public void PassForValidNumber(string value)
    {
        var (valid, error) = validator.Validate(value, Specification(AttributeSpecificationType.Number));

        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("not a number")]
    [InlineData("12.5")]
    public void FailWhenNumberIsInvalid(string value)
    {
        var (valid, error) = validator.Validate(value, Specification(AttributeSpecificationType.Number));

        valid.Should().BeFalse();
        error.Should().Be(AttributeValidationError.NotANumber);
    }

    [Fact]
    public void PassWhenNumberDigitsFitMaxLength()
    {
        var (valid, error) = validator.Validate("123", Specification(AttributeSpecificationType.Number, maxLength: 3));

        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void FailWhenNumberDigitsExceedMaxLength()
    {
        var (valid, error) = validator.Validate("1234", Specification(AttributeSpecificationType.Number, maxLength: 3));

        valid.Should().BeFalse();
        error.Should().Be(AttributeValidationError.StringTooLong(3));
    }

    [Fact]
    public void CountDigitsOfNegativeNumberWithoutSign()
    {
        // Math.Abs strips the sign, so -123 fits MaxLength 3
        var (valid, error) = validator.Validate("-123", Specification(AttributeSpecificationType.Number, maxLength: 3));

        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void FailWhenNegativeNumberDigitsExceedMaxLength()
    {
        var (valid, error) = validator.Validate("-1234", Specification(AttributeSpecificationType.Number, maxLength: 3));

        valid.Should().BeFalse();
        error.Should().Be(AttributeValidationError.StringTooLong(3));
    }

    [Theory]
    [InlineData(AttributeSpecificationType.Text)]
    [InlineData(AttributeSpecificationType.BbCode)]
    public void FailWhenTextExceedsMaxLength(AttributeSpecificationType type)
    {
        var (valid, error) = validator.Validate(new string('a', 11), Specification(type, maxLength: 10));

        valid.Should().BeFalse();
        error.Should().Be(AttributeValidationError.StringTooLong(10));
    }

    [Theory]
    [InlineData(AttributeSpecificationType.Text)]
    [InlineData(AttributeSpecificationType.BbCode)]
    public void PassWhenTextFitsMaxLength(AttributeSpecificationType type)
    {
        var (valid, error) = validator.Validate(new string('a', 10), Specification(type, maxLength: 10));

        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(AttributeSpecificationType.Text)]
    [InlineData(AttributeSpecificationType.BbCode)]
    public void PassWhenTextHasNoMaxLength(AttributeSpecificationType type)
    {
        var (valid, error) = validator.Validate(new string('a', 5000), Specification(type));

        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(AttributeSpecificationType.TextList)]
    [InlineData(AttributeSpecificationType.NumberList)]
    [InlineData(AttributeSpecificationType.TextNumberList)]
    public void PassWhenListValueIsPresentInList(AttributeSpecificationType type)
    {
        var (valid, error) = validator.Validate("Sword",
            Specification(type, listValues: ["Sword", "Bow"]));

        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(AttributeSpecificationType.TextList)]
    [InlineData(AttributeSpecificationType.NumberList)]
    [InlineData(AttributeSpecificationType.TextNumberList)]
    public void FailWhenListValueIsAbsentFromList(AttributeSpecificationType type)
    {
        var (valid, error) = validator.Validate("Axe",
            Specification(type, listValues: ["Sword", "Bow"]));

        valid.Should().BeFalse();
        error.Should().Be(AttributeValidationError.NotPresentInList(["Sword", "Bow"]));
    }

    [Fact]
    public void FailForUndefinedSpecificationType()
    {
        var (valid, error) = validator.Validate("anything",
            Specification((AttributeSpecificationType)99));

        valid.Should().BeFalse();
        error.Should().Be(ValidationError.Invalid);
    }
}
