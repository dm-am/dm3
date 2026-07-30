using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentAssertions;
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
}
