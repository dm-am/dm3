using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

public class GameIntentionResolverShould : UnitTestBase
{
    private readonly GameIntentionResolver resolver;

    public GameIntentionResolverShould()
    {
        resolver = new GameIntentionResolver();
    }

    [Fact]
    public void AllowCreateGameForAuthenticatedUser()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        resolver.IsAllowed(user, GameIntention.Create).Should().BeTrue();
    }

    [Fact]
    public void ForbidCreateGameForGuest()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Create).Should().BeFalse();
    }

    [Fact]
    public void AllowSubscribeForAuthenticatedUser()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        resolver.IsAllowed(user, GameIntention.Subscribe).Should().BeTrue();
    }

    [Fact]
    public void AllowSetStatusModerationForMentor()
    {
        var user = Create.User().WithRole(UserRole.Mentor).Please();
        resolver.IsAllowed(user, GameIntention.SetStatusModeration).Should().BeTrue();
    }

    [Fact]
    public void ForbidSetStatusModerationForPlayer()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        resolver.IsAllowed(user, GameIntention.SetStatusModeration).Should().BeFalse();
    }

    [Fact]
    public void AllowReadPublicGameForAnyone()
    {
        var game = Create.Game()
            .WithStatus(ModuleStatus.Active)
            .WithPremoderationStatus(PremoderationStatus.Approved)
            .Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Read, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidReadDraftGameForGuest()
    {
        var game = Create.Game()
            .WithStatus(ModuleStatus.Draft)
            .Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Read, game).Should().BeFalse();
    }

    [Fact]
    public void AllowReadDraftGameForMaster()
    {
        var masterId = Guid.NewGuid();
        var game = Create.Game()
            .WithMaster(masterId)
            .WithStatus(ModuleStatus.Draft)
            .Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Read, game).Should().BeTrue();
    }

    [Fact]
    public void AllowEditGameForMaster()
    {
        var masterId = Guid.NewGuid();
        var game = Create.Game()
            .WithMaster(masterId)
            .Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Edit, game).Should().BeTrue();
    }

    [Fact]
    public void AllowEditGameForAssistant()
    {
        var assistantId = Guid.NewGuid();
        var game = Create.Game()
            .WithAssistants(assistantId)
            .Please();
        var user = Create.User(assistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Edit, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidEditGameForPlayer()
    {
        var playerId = Guid.NewGuid();
        var game = Create.Game()
            .WithPlayers(playerId)
            .Please();
        var user = Create.User(playerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Edit, game).Should().BeFalse();
    }

    [Fact]
    public void AllowDeleteGameForMaster()
    {
        var masterId = Guid.NewGuid();
        var game = Create.Game()
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
        var game = Create.Game()
            .WithMaster(masterId)
            .WithAssistants(assistantId)
            .Please();
        var user = Create.User(assistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Delete, game).Should().BeFalse();
    }

    [Fact]
    public void AllowReadCommentsWhenAccessModeIsPublic()
    {
        var game = Create.Game()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.ReadComments, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidReadCommentsWhenAccessModeIsPrivateAndUserHasNoRole()
    {
        var game = Create.Game()
            .WithCommentsAccessMode(CommentsAccessMode.Private)
            .Please();
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.ReadComments, game).Should().BeFalse();
    }

    [Fact]
    public void AllowReadPrivateCommentsForPlayer()
    {
        var playerId = Guid.NewGuid();
        var game = Create.Game()
            .WithCommentsAccessMode(CommentsAccessMode.Private)
            .WithPlayers(playerId)
            .Please();
        var user = Create.User(playerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.ReadComments, game).Should().BeTrue();
    }
}
