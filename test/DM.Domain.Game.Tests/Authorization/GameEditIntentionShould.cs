using System;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Testing.Dsl;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Who may edit a game, open its settings page and delete it.
/// </summary>
public class GameEditIntentionShould
{
    private readonly GameIntentionResolver resolver = new();

    [Theory]
    [InlineData(GameIntention.Edit, true)]
    [InlineData(GameIntention.Edit, false)]
    [InlineData(GameIntention.EditSettings, true)]
    [InlineData(GameIntention.EditSettings, false)]
    public void AllowTheLeadsToEditTheGameAndItsSettings(GameIntention intention, bool asMaster)
    {
        var leadId = Guid.NewGuid();
        var game = asMaster
            ? new GameBuilder().WithMaster(leadId).Please()
            : new GameBuilder().WithAssistants(leadId).Please();
        var user = Create.User(leadId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidEditGameForPlayer()
    {
        var playerId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithPlayers(playerId)
            .Please();
        var user = Create.User(playerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Edit, game).Should().BeFalse();
    }

    /// <summary>
    /// The curator helps set the game up, so the settings page is open to them.
    /// </summary>
    [Fact]
    public void AllowEditSettingsForTheMentorCuratingTheGame()
    {
        var mentorId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMentor(mentorId)
            .Please();
        var user = Create.User(mentorId).WithRole(UserRole.Mentor).Please();

        resolver.IsAllowed(user, GameIntention.EditSettings, game).Should().BeTrue();
    }

    /// <summary>
    /// The other half of the settings page stays shut for the curator. Rooms and
    /// the blacklist ask for <see cref="GameIntention.Edit" />, the roster asks
    /// for its own invite and removal intentions, and none of them widened when
    /// the information form did. A settings page that offered these controls to
    /// a mentor would be promising what this theory refuses.
    /// </summary>
    [Theory]
    [InlineData(GameIntention.Edit)]
    [InlineData(GameIntention.InvitePlayer)]
    [InlineData(GameIntention.InviteReader)]
    [InlineData(GameIntention.InviteAssistant)]
    [InlineData(GameIntention.CancelInvitation)]
    [InlineData(GameIntention.RemoveUser)]
    [InlineData(GameIntention.Delete)]
    public void ForbidTheCuratingMentorEverythingBeyondTheSettingsForm(GameIntention intention)
    {
        var mentorId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMentor(mentorId)
            .Please();
        var user = Create.User(mentorId).WithRole(UserRole.Mentor).Please();

        resolver.IsAllowed(user, intention, game).Should().BeFalse();
    }

    /// <summary>
    /// The site rank staffs the review queue, it does not open every game's
    /// settings: what admits a mentor here is the assignment, same as Read.
    /// </summary>
    [Fact]
    public void ForbidEditSettingsForAMentorWhoDoesNotCurateTheGame()
    {
        var game = new GameBuilder()
            .WithMentor(Guid.NewGuid())
            .Please();
        var user = Create.User().WithRole(UserRole.Mentor).Please();

        resolver.IsAllowed(user, GameIntention.EditSettings, game).Should().BeFalse();
    }

    /// <summary>
    /// A senior moderator already edits the game, so the page cannot be narrower
    /// for them than the operations behind it.
    /// </summary>
    [Fact]
    public void AllowEditSettingsForSeniorModerator()
    {
        var game = new GameBuilder().Please();
        var user = Create.User().WithRole(UserRole.SeniorModerator).Please();

        resolver.IsAllowed(user, GameIntention.EditSettings, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidEditSettingsForStranger()
    {
        var game = new GameBuilder().Please();
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.EditSettings, game).Should().BeFalse();
    }

    [Fact]
    public void ForbidEditSettingsForGuest()
    {
        var game = new GameBuilder().Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.EditSettings, game).Should().BeFalse();
    }

    [Fact]
    public void AllowDeleteGameForMaster()
    {
        var masterId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Delete, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidDeleteGameForAssistant()
    {
        var masterId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .WithAssistants(assistantId)
            .Please();
        var user = Create.User(assistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Delete, game).Should().BeFalse();
    }
}
