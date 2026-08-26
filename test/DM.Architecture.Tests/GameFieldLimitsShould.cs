using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using DM.Domain.Core.Content;
using DM.Web.API.Features.Game.Games;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The contract and the domain check a game's text fields by the same numbers.
/// </summary>
/// <remarks>
/// They did not. The request contract allowed a 200-character title, a
/// 100-character system and setting, and a description of one character; the
/// domain validator behind it cut the title at 100, the other two at 50, and
/// demanded at least 200 characters of description. Everything between the two
/// sets was accepted by the form, accepted by the contract and refused by the
/// domain — and refused in a code the form had no line for, so the author saw an
/// English word under a Russian field after pressing "create".
///
/// Asserted through the attributes rather than by reading source text: the
/// contract states its bounds in DataAnnotations, and an attribute carrying a
/// literal is exactly the drift this rule exists to catch.
/// </remarks>
public class GameFieldLimitsShould
{
    [Theory]
    [InlineData(nameof(CreateGameRequest.Title), GameFieldLimits.TitleMaxLength)]
    [InlineData(nameof(CreateGameRequest.System), GameFieldLimits.SystemMaxLength)]
    [InlineData(nameof(CreateGameRequest.Setting), GameFieldLimits.SettingMaxLength)]
    public void BoundTheRequestByTheSharedMaximum(string property, int expected)
    {
        MaximumOf(typeof(CreateGameRequest), property).Should().Be(expected,
            "the form draws its maxlength from this contract, and the domain refuses " +
            "anything the contract lets through above its own bound");
    }

    [Fact]
    public void AskTheRequestForTheMinimumTheDomainEnforces()
    {
        var info = typeof(CreateGameRequest).GetProperty(nameof(CreateGameRequest.Info))!;
        var minimum = info.GetCustomAttributes<MinLengthAttribute>().Single().Length;

        minimum.Should().Be(GameFieldLimits.InfoMinLength,
            "a short description has been refused by the domain all along; the contract " +
            "promising that one character is enough is what made the refusal a surprise");
    }

    /// <summary>
    /// A rule over numbers only holds while the numbers are read from one place.
    /// </summary>
    [Fact]
    public void KeepTheLimitsWorthReading()
    {
        GameFieldLimits.TitleMinLength.Should().BeLessThan(GameFieldLimits.TitleMaxLength);
        GameFieldLimits.InfoMinLength.Should().BeGreaterThan(0,
            "a minimum of zero is no rule, and the form would have nothing to say");
    }

    private static int MaximumOf(Type contract, string property)
    {
        var member = contract.GetProperty(property)
            ?? throw new InvalidOperationException($"{contract.Name} has no {property}");

        var stringLength = member.GetCustomAttributes<StringLengthAttribute>().SingleOrDefault();
        if (stringLength != null) return stringLength.MaximumLength;

        return member.GetCustomAttributes<MaxLengthAttribute>().Single().Length;
    }
}
