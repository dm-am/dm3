using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameRoleExtensionsShould : UnitTestBase
{
    /// <summary>
    /// The roles the ordinary ban's exemption is meant to cover: the two that
    /// lead the game, the mentor curating it, and a player already accepted.
    /// </summary>
    [Theory]
    [InlineData(GameRole.Master)]
    [InlineData(GameRole.Assistant)]
    [InlineData(GameRole.Mentor)]
    [InlineData(GameRole.Player)]
    public void CountLeadingAndPlayingAsOwnGame(GameRole role)
    {
        new[] { role }.IsOwnGame().Should().BeTrue();
    }

    /// <summary>
    /// Written as "anything that is not a Reader", the predicate answered true
    /// for every other member of the enum, including the ones nobody resolves
    /// yet. The exemption then widens on its own: on the day Applicant starts
    /// being resolved, and with every role added to the enum after it.
    /// </summary>
    [Theory]
    [InlineData(GameRole.None)]
    [InlineData(GameRole.Reader)]
    [InlineData(GameRole.Applicant)]
    public void NotCountReadingOrApplyingAsOwnGame(GameRole role)
    {
        new[] { role }.IsOwnGame().Should().BeFalse();
    }

    /// <summary>
    /// Every role has a name on the wire. Three of the seven collapsed into
    /// "unknown", and the mentor is returned by the roster query, so the client
    /// got a value its own union type does not contain.
    /// </summary>
    [Fact]
    public void RenderEveryRoleUnderItsOwnName()
    {
        var names = Enum.GetValues<GameRole>().Select(role => role.ToApiString()).ToArray();

        names.Should().NotContain("unknown");
        names.Should().OnlyHaveUniqueItems("two roles under one name make the filter ambiguous");
    }

    /// <summary>
    /// The filter reads the table the response is written with. It kept its own
    /// literals, and "mentor" was not among the produced ones, so ?role=mentor
    /// answered empty for a game that has one.
    /// </summary>
    [Fact]
    public void ParseBackEveryNameItRenders()
    {
        foreach (var role in Enum.GetValues<GameRole>())
        {
            GameRoleExtensions.TryParseApiString(role.ToApiString(), out var parsed).Should().BeTrue();
            parsed.Should().Be(role);
        }

        GameRoleExtensions.TryParseApiString("formerPlayer", out _).Should().BeFalse(
            "the model has no such role, and a filter naming one answers empty forever");
    }
}
