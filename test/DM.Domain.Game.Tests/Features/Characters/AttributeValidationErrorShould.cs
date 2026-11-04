using System;
using DM.Domain.Core.Extensions;
using DM.Domain.Game.Features.Characters;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

/// <summary>
/// The sentences a player reads when a character sheet is refused. The other tests compare
/// against the constants of <see cref="AttributeValidationError"/> and would pass for any
/// words; these hold the words themselves, in Russian and with a name for the thing refused.
/// </summary>
public class AttributeValidationErrorShould
{
    [Fact]
    public void RefuseAnAttributeTheSchemaDoesNotHave() =>
        AttributeValidationError.InvalidSpecification.Should().Be("Такого атрибута нет в схеме игры");

    [Fact]
    public void RefuseAMissingRequiredAttribute() =>
        AttributeValidationError.RequiredMissing.Should().Be("Атрибут обязателен");

    [Fact]
    public void NameEveryRequiredAttributeThatIsMissing()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        AttributeValidationError.ManyRequiredMissing([(first, "Сила"), (second, "Ловкость")])
            .Should().Be(
                $"Не заполнены обязательные атрибуты: {first.EncodeToReadable()} (Сила), " +
                $"{second.EncodeToReadable()} (Ловкость)");
    }

    [Fact]
    public void NameEveryAttributeSubmittedMoreThanOnce()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        AttributeValidationError.ManyDuplicated([first, second])
            .Should().Be(
                $"Атрибуты переданы больше одного раза: {first.EncodeToReadable()}, {second.EncodeToReadable()}");
    }

    [Fact]
    public void AskForANumberWhereOneIsExpected() =>
        AttributeValidationError.NotANumber.Should().Be("Нужно число");

    [Fact]
    public void NameTheLimitOfATextThatIsTooLong() =>
        AttributeValidationError.StringTooLong(120).Should().Be("Не длиннее 120 символов");

    [Fact]
    public void ListTheValuesAListAttributeAccepts() =>
        AttributeValidationError.NotPresentInList(["Меч", "Лук"])
            .Should().Be("Выберите одно из значений: Меч, Лук");
}
