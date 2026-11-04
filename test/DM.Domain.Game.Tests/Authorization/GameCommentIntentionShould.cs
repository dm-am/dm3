using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Testing.Dsl;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Who may write in the discussion of a game: under a ban, as an applicant and
/// from the game's blacklist, which closes the application as well and leaves
/// the reading open.
/// </summary>
public class GameCommentIntentionShould
{
    private readonly GameIntentionResolver resolver = new();

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
    public void AllowReadOfAPublicGameForTheUserItBlacklisted()
    {
        var blacklistedId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Active)
            .WithPremoderationStatus(PremoderationStatus.Approved)
            .WithBlacklisted(blacklistedId)
            .Please();
        var user = Create.User(blacklistedId).WithRole(UserRole.RegularUser).Please();

        // The blacklist closes writing, not reading: the game is public to everybody
        // else, so hiding it from one person would promise a privacy it does not have.
        resolver.IsAllowed(user, GameIntention.Read, game).Should().BeTrue();
    }

    [Fact]
    public void ForbidCommentingAGameForTheUserItBlacklisted()
    {
        var blacklistedId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithCommentsAccessMode(CommentsAccessMode.Public)
            .WithBlacklisted(blacklistedId)
            .Please();
        var user = Create.User(blacklistedId).WithRole(UserRole.RegularUser).Please();

        // The other half of the same rule: they may read the game and may not
        // write in it
        resolver.IsAllowed(user, GameIntention.CreateComment, game).Should().BeFalse();
    }

    /// <summary>
    /// An application is writing into the game, whichever door it comes
    /// through: open recruitment or a player invitation. The gate carries the
    /// rule so that IsAllowed does not answer "yes" to somebody the service
    /// then refuses.
    /// </summary>
    [Fact]
    public void ForbidApplyingToAGameForTheUserItBlacklisted()
    {
        var blacklistedId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Active)
            .WithRecruitmentOpen()
            .WithPendingPlayerInvitation(blacklistedId)
            .WithBlacklisted(blacklistedId)
            .Please();
        var user = Create.User(blacklistedId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.CreateCharacter, game).Should().BeFalse();
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
