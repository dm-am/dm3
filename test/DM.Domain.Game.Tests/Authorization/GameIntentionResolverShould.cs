using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Testing.Dsl;
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
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Active)
            .WithPremoderationStatus(PremoderationStatus.Approved)
            .Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Read, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidReadDraftGameForGuest()
    {
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithDraftVisibility(DraftVisibility.Private)
            .Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Read, game).Should().BeFalse();
    }

    [Fact]
    public void AllowReadDraftGameWithPublicPreviewForGuest()
    {
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithDraftVisibility(DraftVisibility.Public)
            .Please();

        // The game list is filtered by the same rule and shows this game to
        // everyone, so opening it must not answer with a refusal
        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Read, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidReadDraftGameWithPublicPreviewAwaitingPremoderation()
    {
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithDraftVisibility(DraftVisibility.Public)
            .WithPremoderationStatus(PremoderationStatus.AwaitingApproval)
            .Please();

        // The preview setting does not outrank premoderation: a newbie master
        // cannot publish a game around the curator by opening the preview
        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Read, game).Should().BeFalse();
    }

    [Fact]
    public void AllowReadDraftGameForMaster()
    {
        var masterId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .WithStatus(ModuleStatus.Draft)
            .Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Read, game).Should().BeTrue();
    }

    [Fact]
    public void AllowReadOfAPremoderatedGameForTheMentorCuratingIt()
    {
        var mentorId = Guid.NewGuid();
        // A newbie's game while it waits for review: hidden as a draft and hidden
        // by premoderation, with the curator already assigned to it.
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithPremoderationStatus(PremoderationStatus.AwaitingApproval)
            .WithMentor(mentorId)
            .Please();
        var user = Create.User(mentorId).WithRole(UserRole.Mentor).Please();

        // The moderation queue links straight to the game page and the SQL scope
        // already hands the row to the assigned mentor, so refusing here answered
        // 403 on the one game the link exists for.
        resolver.IsAllowed(user, GameIntention.Read, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidReadOfAPremoderatedGameForAMentorWhoDoesNotCurateIt()
    {
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithPremoderationStatus(PremoderationStatus.AwaitingApproval)
            .WithMentor(Guid.NewGuid())
            .Please();
        var user = Create.User().WithRole(UserRole.Mentor).Please();

        // The site role staffs the review queue, it does not open every hidden
        // game: what admits a mentor is the assignment, not the rank.
        resolver.IsAllowed(user, GameIntention.Read, game).Should().BeFalse();
    }

    [Fact]
    public void AllowEditGameForMaster()
    {
        var masterId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Edit, game).Should().BeTrue();
    }

    [Fact]
    public void AllowEditGameForAssistant()
    {
        var assistantId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithAssistants(assistantId)
            .Please();
        var user = Create.User(assistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Edit, game).Should().BeTrue();
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

    [Fact]
    public void AllowReadCommentsWhenAccessModeIsPublic()
    {
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.ReadComments, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidReadCommentsWhenAccessModeIsPrivateAndUserHasNoRole()
    {
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Private)
            .Please();
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.ReadComments, game).Should().BeFalse();
    }

    [Fact]
    public void AllowReadPrivateCommentsForPlayer()
    {
        var playerId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Private)
            .WithPlayers(playerId)
            .Please();
        var user = Create.User(playerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.ReadComments, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidCommentingSomebodyElsesGameUnderTheOrdinaryBan()
    {
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .Please();
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.DemocraticBan)
            .Please();

        resolver.IsAllowed(user, GameIntention.CreateComment, game).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void AllowCommentingOwnGameUnderTheOrdinaryBan(bool asMaster, bool asAssistant, bool asPlayer)
    {
        var userId = Guid.NewGuid();
        var builder = new GameBuilder().WithCommentsAccessMode(CommentsAccessMode.Public);
        if (asMaster) builder = builder.WithMaster(userId);
        if (asAssistant) builder = builder.WithAssistants(userId);
        if (asPlayer) builder = builder.WithPlayers(userId);

        var user = Create.User(userId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.DemocraticBan)
            .Please();

        // Being accepted into a game keeps its discussion open under the
        // ordinary ban, whichever way the user belongs to it
        resolver.IsAllowed(user, GameIntention.CreateComment, builder.Please()).Should().BeTrue();
    }

    [Fact]
    public void NotTreatASubscriptionAsOwningTheGameUnderTheOrdinaryBan()
    {
        var userId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .WithViewerSubscribed()
            .Please();
        var user = Create.User(userId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.DemocraticBan)
            .Please();

        // Subscribing is one self-service request against any public game. If it
        // counted as belonging, the ban would be undone by pressing a button.
        resolver.IsAllowed(user, GameIntention.CreateComment, game).Should().BeFalse();
    }

    [Fact]
    public void NotLetAnApplicantCommentUntilTheCharacterIsAccepted()
    {
        var applicantId = Guid.NewGuid();
        // An application in review puts nobody into Players — that list is built
        // from authors of Active non-NPC characters only. Applying may also leave
        // a subscription behind, which must not stand in for acceptance either.
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .WithViewerSubscribed()
            .Please();
        var user = Create.User(applicantId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.DemocraticBan)
            .Please();

        resolver.IsAllowed(user, GameIntention.CreateComment, game).Should().BeFalse();
    }

    [Fact]
    public void LetTheApplicantCommentOnceTheCharacterIsAccepted()
    {
        var playerId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .WithViewerSubscribed()
            .WithPlayers(playerId)
            .Please();
        var user = Create.User(playerId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.DemocraticBan)
            .Please();

        // Acceptance is what changes the answer, and only acceptance
        resolver.IsAllowed(user, GameIntention.CreateComment, game).Should().BeTrue();
    }

    [Fact]
    public void LetTheCuratorCommentInTheGameTheyMentorUnderTheOrdinaryBan()
    {
        var mentorId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .WithMentor(mentorId)
            .Please();
        var user = Create.User(mentorId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.DemocraticBan)
            .Please();

        // Curating is a job in the game: a mentor speaks in the game they
        // supervise. Being a game lead for [private] visibility is a different
        // question and stays master + assistant.
        resolver.IsAllowed(user, GameIntention.CreateComment, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidCommentingOwnGameUnderAFullBan()
    {
        var masterId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .Please();
        var user = Create.User(masterId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.FullBan)
            .Please();

        resolver.IsAllowed(user, GameIntention.CreateComment, game).Should().BeFalse();
    }
}
