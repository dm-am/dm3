using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameRoleExtensionsShould
{
    // The roles the ordinary ban's exemption is meant to cover: the two that lead the game, the
    // mentor curating it, and a player already accepted.
    [Theory]
    [InlineData(GameRole.Master)]
    [InlineData(GameRole.Assistant)]
    [InlineData(GameRole.Mentor)]
    [InlineData(GameRole.Player)]
    public void CountLeadingAndPlayingAsOwnGame(GameRole role)
    {
        new[] { role }.IsOwnGame().Should().BeTrue();
    }

    // Written as "anything that is not a Reader", the predicate answered true for None, and the
    // exemption widened on its own with every role added to the enum after it.
    [Theory]
    [InlineData(GameRole.None)]
    [InlineData(GameRole.Reader)]
    public void NotCountReadingAsOwnGame(GameRole role)
    {
        new[] { role }.IsOwnGame().Should().BeFalse();
    }

    // Three of the seven collapsed into "unknown", and the mentor is returned by the roster query,
    // so the client got a value its own union type does not contain.
    [Fact]
    public void RenderEveryRoleUnderItsOwnName()
    {
        var names = Enum.GetValues<GameRole>().Select(role => role.ToApiString()).ToArray();

        names.Should().NotContain("unknown");
        names.Should().OnlyHaveUniqueItems("two roles under one name make the filter ambiguous");
    }
}
