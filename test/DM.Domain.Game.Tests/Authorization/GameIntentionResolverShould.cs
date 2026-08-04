using System;
using System.Linq;
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
    public void AllowReadOfAPublicGameForTheUserItBlacklisted()
    {
        var blacklistedId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Active)
            .WithPremoderationStatus(PremoderationStatus.Approved)
            .WithBlacklisted(blacklistedId)
            .Please();
        var user = Create.User(blacklistedId).WithRole(UserRole.RegularUser).Please();

        // The blacklist closes writing, not reading. The game is public to
        // everybody else, so hiding it from one person would promise a privacy it
        // does not have, and GameAccessibilityFilters answers the list the same
        // way now: the two used to disagree.
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

    /// <summary>
    /// Who may invite whom, and who may remove a user.
    /// </summary>
    /// <remarks>
    /// Five neighbouring arms, three reading HasEditAccess (master or assistant)
    /// and two reading Contains(Master). Nothing exercised any of them, so
    /// folding the two into the three - the obvious tidy - would have handed
    /// every assistant the right to appoint and remove other assistants, and
    /// left the suite green.
    /// </remarks>
    [Theory]
    [InlineData(GameIntention.InvitePlayer, GameRole.Master, true)]
    [InlineData(GameIntention.InvitePlayer, GameRole.Assistant, true)]
    [InlineData(GameIntention.InvitePlayer, GameRole.Player, false)]
    [InlineData(GameIntention.InvitePlayer, GameRole.None, false)]
    [InlineData(GameIntention.InviteReader, GameRole.Master, true)]
    [InlineData(GameIntention.InviteReader, GameRole.Assistant, true)]
    [InlineData(GameIntention.InviteReader, GameRole.Player, false)]
    [InlineData(GameIntention.InviteReader, GameRole.None, false)]
    [InlineData(GameIntention.CancelInvitation, GameRole.Master, true)]
    [InlineData(GameIntention.CancelInvitation, GameRole.Assistant, true)]
    [InlineData(GameIntention.CancelInvitation, GameRole.Player, false)]
    [InlineData(GameIntention.CancelInvitation, GameRole.None, false)]
    [InlineData(GameIntention.InviteAssistant, GameRole.Master, true)]
    [InlineData(GameIntention.InviteAssistant, GameRole.Assistant, false)]
    [InlineData(GameIntention.InviteAssistant, GameRole.Player, false)]
    [InlineData(GameIntention.InviteAssistant, GameRole.None, false)]
    [InlineData(GameIntention.RemoveUser, GameRole.Master, true)]
    [InlineData(GameIntention.RemoveUser, GameRole.Assistant, false)]
    [InlineData(GameIntention.RemoveUser, GameRole.Player, false)]
    [InlineData(GameIntention.RemoveUser, GameRole.None, false)]
    public void DecideAnInvitationByTheSeatTheRuleNames(
        GameIntention intention, GameRole seat, bool expected)
    {
        var userId = Guid.NewGuid();
        var builder = new GameBuilder();
        builder = seat switch
        {
            GameRole.Master => builder.WithMaster(userId),
            GameRole.Assistant => builder.WithAssistants(userId),
            GameRole.Player => builder.WithPlayers(userId),
            _ => builder
        };
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, builder.Please()).Should().Be(expected);
    }

    /// <summary>
    /// A status change is decided from the status the game is in.
    /// </summary>
    /// <remarks>
    /// Four arms, each guarded by the current status, and no test named any of
    /// them: swapping two guards is a change no assertion could see, and the
    /// first report of it would be a master answered 403 on publishing a game.
    /// </remarks>
    [Theory]
    [InlineData(ModuleStatus.Draft, GameIntention.SetStatusActive, true)]
    [InlineData(ModuleStatus.Active, GameIntention.SetStatusActive, false)]
    [InlineData(ModuleStatus.Closed, GameIntention.SetStatusActive, true)]
    [InlineData(ModuleStatus.Active, GameIntention.SetStatusDraft, true)]
    [InlineData(ModuleStatus.Draft, GameIntention.SetStatusDraft, false)]
    [InlineData(ModuleStatus.Closed, GameIntention.SetStatusDraft, false)]
    [InlineData(ModuleStatus.Active, GameIntention.SetStatusClosed, true)]
    [InlineData(ModuleStatus.Closed, GameIntention.SetStatusClosed, true)]
    [InlineData(ModuleStatus.Draft, GameIntention.SetStatusClosed, false)]
    public void AllowAStatusChangeOnlyFromTheStatusItStartsIn(
        ModuleStatus current, GameIntention intention, bool expected)
    {
        var masterId = Guid.NewGuid();
        var game = new GameBuilder().WithMaster(masterId).WithStatus(current).Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, game).Should().Be(expected);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft, GameIntention.SetStatusActive)]
    [InlineData(ModuleStatus.Active, GameIntention.SetStatusDraft)]
    [InlineData(ModuleStatus.Active, GameIntention.SetStatusClosed)]
    public void RefuseAStatusChangeToAPlayer(ModuleStatus current, GameIntention intention)
    {
        var playerId = Guid.NewGuid();
        var game = new GameBuilder().WithPlayers(playerId).WithStatus(current).Please();
        var user = Create.User(playerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, game).Should().BeFalse();
    }

    /// <summary>
    /// Every intention the enum declares is granted to somebody.
    /// </summary>
    /// <remarks>
    /// Nine of the nineteen members were named in no test at all. This is the
    /// gate that keeps the count from sliding back: an arm that stops granting
    /// anything - the shape a careless simplification takes - and a member added
    /// with no rule behind it both land here. The seats and states below are the
    /// ones the arms are written for; add a member and this is red until its case
    /// joins them.
    /// </remarks>
    [Fact]
    public void GrantEveryIntentionTheEnumDeclaresToSomebody()
    {
        var userId = Guid.NewGuid();
        var actors = new[]
        {
            AuthenticatedUser.Guest,
            Create.User(userId).WithRole(UserRole.RegularUser).Please(),
            Create.User(userId).WithRole(UserRole.Mentor).Please(),
            Create.User(userId).WithRole(UserRole.SeniorModerator).Please()
        };

        var targets = new[]
        {
            new GameBuilder().WithMaster(userId).WithRecruitmentOpen().Please(),
            new GameBuilder().WithAssistants(userId).WithRecruitmentOpen().Please(),
            new GameBuilder().WithPlayers(userId).Please(),
            new GameBuilder().WithMentor(userId).Please(),
            new GameBuilder().WithViewerSubscribed().Please(),
            new GameBuilder().Please(),
            new GameBuilder().WithMaster(userId).WithStatus(ModuleStatus.Draft).Please(),
            new GameBuilder().WithMentor(userId).WithStatus(ModuleStatus.Draft)
                .WithPremoderationStatus(PremoderationStatus.AwaitingApproval).Please(),
            new GameBuilder().WithMaster(userId).WithStatus(ModuleStatus.Closed).Please(),
            new GameBuilder().WithPendingPlayerInvitation(userId).Please()
        };

        var ungrantable = Enum.GetValues<GameIntention>()
            // Create is the one intention decided without a target: the game
            // does not exist yet, and the subject-only overload answers it.
            .Where(intention => intention != GameIntention.Create)
            .Where(intention => !actors.Any(actor =>
                targets.Any(target => resolver.IsAllowed(actor, intention, target))))
            .Select(intention => intention.ToString())
            .ToArray();

        ungrantable.Should().BeEmpty(
            "an intention nobody can ever be granted is a rule with no subject");
        resolver.IsAllowed(actors[1], GameIntention.Create).Should().BeTrue();
    }
}
