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
/// <remarks>Asserted through the attributes: the contract states its bounds in DataAnnotations.</remarks>
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


    private static int MaximumOf(Type contract, string property)
    {
        var member = contract.GetProperty(property)
            ?? throw new InvalidOperationException($"{contract.Name} has no {property}");

        var stringLength = member.GetCustomAttributes<StringLengthAttribute>().SingleOrDefault();
        if (stringLength != null) return stringLength.MaximumLength;

        return member.GetCustomAttributes<MaxLengthAttribute>().Single().Length;
    }
}
