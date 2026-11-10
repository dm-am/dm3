using System;
using System.Linq;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Testing.Dsl;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

public class GameIntentionResolverShould
{
    private readonly GameIntentionResolver resolver = new();

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
    public void ForbidAGuestToApplyWithACharacterToAnActiveGameWithOpenRecruitment()
    {
        var game = new GameBuilder().WithStatus(ModuleStatus.Active).WithRecruitmentOpen().Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.CreateCharacter, game)
            .Should().BeFalse();
    }

    // A premoderation verdict is a rank and nothing else: the mentor and everybody above one in
    // moderation, whatever their relationship to the game.
    [Theory]
    [InlineData(UserRole.Mentor, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.SeniorModerator, true)]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.RegularUser, false)]
    public void AllowAPremoderationVerdictFromTheMentorRankUpwards(UserRole role, bool expected)
    {
        var user = Create.User().WithRole(role).Please();
        resolver.IsAllowed(user, GameIntention.SetStatusModeration).Should().Be(expected);
    }

    [Fact]
    public void RefuseAPremoderationVerdictToAGuest()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.SetStatusModeration)
            .Should().BeFalse();
    }

    // The curator arm cannot cover this: a game in AwaitingEdits, where every newbie's game starts,
    // records no curator.
    [Theory]
    [InlineData(PremoderationStatus.AwaitingEdits, UserRole.Mentor, true)]
    [InlineData(PremoderationStatus.AwaitingEdits, UserRole.Moderator, true)]
    [InlineData(PremoderationStatus.AwaitingEdits, UserRole.SeniorModerator, true)]
    [InlineData(PremoderationStatus.AwaitingEdits, UserRole.Admin, true)]
    [InlineData(PremoderationStatus.AwaitingEdits, UserRole.RegularUser, false)]
    [InlineData(PremoderationStatus.AwaitingApproval, UserRole.Mentor, true)]
    [InlineData(PremoderationStatus.AwaitingApproval, UserRole.Moderator, true)]
    [InlineData(PremoderationStatus.AwaitingApproval, UserRole.SeniorModerator, true)]
    [InlineData(PremoderationStatus.AwaitingApproval, UserRole.Admin, true)]
    [InlineData(PremoderationStatus.AwaitingApproval, UserRole.RegularUser, false)]
    public void OpenAPremoderatedGameToExactlyTheRanksThatMayJudgeIt(
        PremoderationStatus premoderationStatus, UserRole role, bool expected)
    {
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Active)
            .WithPremoderationStatus(premoderationStatus)
            .Please();
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, GameIntention.Read, game).Should().Be(expected);

        // The two halves of one right: seeing the game and moving it along
        // premoderation answer the same for a reader who holds no role in it.
        resolver.IsAllowed(user, GameIntention.Read, game)
            .Should().Be(resolver.IsAllowed(user, GameIntention.SetStatusModeration));
    }

    [Theory]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    public void KeepAPremoderatedGameShutToAGuest(PremoderationStatus premoderationStatus)
    {
        var game = new GameBuilder()
            .WithStatus(ModuleStatus.Active)
            .WithPremoderationStatus(premoderationStatus)
            .Please();

        resolver.IsAllowed(AuthenticatedUser.Guest, GameIntention.Read, game).Should().BeFalse();
    }

    // A player of somebody else's game is a stranger to this one: the rank arm is a rank and the
    // role arms are about this game, and neither of them is a claim about games the reader plays
    // elsewhere.
    [Fact]
    public void KeepAPremoderatedGameShutToAPlayerOfAnotherGame()
    {
        var playerId = Guid.NewGuid();
        var otherGame = new GameBuilder().WithPlayers(playerId).Please();
        var premoderatedGame = new GameBuilder()
            .WithPremoderationStatus(PremoderationStatus.AwaitingEdits)
            .Please();
        var user = Create.User(playerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.Read, otherGame).Should().BeTrue();
        resolver.IsAllowed(user, GameIntention.Read, premoderatedGame).Should().BeFalse();
    }

    // A private draft is hidden by its author's choice, not by premoderation, and no mentor is owed
    // a look at it.
    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void KeepAPrivateDraftShutToTheRanksThatJudgePremoderation(UserRole role)
    {
        var draft = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithPremoderationStatus(PremoderationStatus.Approved)
            .WithDraftVisibility(DraftVisibility.Private)
            .Please();

        resolver.IsAllowed(Create.User().WithRole(role).Please(), GameIntention.Read, draft)
            .Should().BeFalse();
    }

    /// <summary>
    /// Asking for a verdict is the master's move and nobody else's.
    /// </summary>
    /// <remarks>
    /// Not the assistant's, who fills the same form in; not the curating mentor's,
    /// who is on the settings page for exactly that reason; and not senior
    /// moderation's, which may edit the game but does not speak for its author.
    /// This is why the move has an intention of its own instead of riding on
    /// EditSettings.
    /// </remarks>
    [Fact]
    public void AllowSubmitForApprovalToTheMasterAlone()
    {
        var masterId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .WithAssistants(assistantId)
            .WithMentor(mentorId)
            .WithPremoderationStatus(PremoderationStatus.AwaitingEdits)
            .Please();

        resolver.IsAllowed(
            Create.User(masterId).WithRole(UserRole.RegularUser).Please(),
            GameIntention.SubmitForApproval, game).Should().BeTrue();
        resolver.IsAllowed(
            Create.User(assistantId).WithRole(UserRole.RegularUser).Please(),
            GameIntention.SubmitForApproval, game).Should().BeFalse();
        resolver.IsAllowed(
            Create.User(mentorId).WithRole(UserRole.Mentor).Please(),
            GameIntention.SubmitForApproval, game).Should().BeFalse();
        resolver.IsAllowed(
            Create.User().WithRole(UserRole.SeniorModerator).Please(),
            GameIntention.SubmitForApproval, game).Should().BeFalse();
        resolver.IsAllowed(
            AuthenticatedUser.Guest, GameIntention.SubmitForApproval, game).Should().BeFalse();
    }

    /// <summary>
    /// The master may ask from any premoderation status the resolver is shown.
    /// </summary>
    /// <remarks>
    /// Which status the move is legal from is the machine's answer and is given
    /// before this gate is asked, so an author who submits twice hears that the
    /// move is illegal - a 400 naming the status - and not that they are not
    /// themselves. Restating the condition here would turn that into a 403.
    /// </remarks>
    [Theory]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.Approved)]
    public void AnswerSubmitForApprovalOnIdentityAndNotOnStatus(PremoderationStatus current)
    {
        var masterId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .WithPremoderationStatus(current)
            .Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.SubmitForApproval, game).Should().BeTrue();
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
    public void AllowReadOfAPremoderatedGameForAMentorWhoDoesNotCurateIt()
    {
        var curatedByAnother = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithPremoderationStatus(PremoderationStatus.AwaitingApproval)
            .WithMentor(Guid.NewGuid())
            .Please();
        var curatedByNobody = new GameBuilder()
            .WithStatus(ModuleStatus.Draft)
            .WithPremoderationStatus(PremoderationStatus.AwaitingEdits)
            .Please();
        var user = Create.User().WithRole(UserRole.Mentor).Please();

        resolver.IsAllowed(user, GameIntention.Read, curatedByAnother).Should().BeTrue();
        resolver.IsAllowed(user, GameIntention.Read, curatedByNobody).Should().BeTrue();
    }

    // Three arms read HasEditAccess and two read Contains(Master): folding them would let
    // assistants appoint assistants.
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

    // Each arm is guarded by the current status, so swapping two guards must turn a case red.
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

    // An arm that stops granting anything and a member added with no rule behind it both land here.
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

        // The two decided without a target. Create, because the game does not
        // exist yet; SetStatusModeration, because a premoderation verdict is a
        // rank and asking the game about it would let a game grant one. Both are
        // answered by the subject-only overload, asserted below.
        var targetless = new[] { GameIntention.Create, GameIntention.SetStatusModeration };

        var ungrantable = Enum.GetValues<GameIntention>()
            .Where(intention => !targetless.Contains(intention))
            .Where(intention => !actors.Any(actor =>
                targets.Any(target => resolver.IsAllowed(actor, intention, target))))
            .Select(intention => intention.ToString())
            .ToArray();

        ungrantable.Should().BeEmpty(
            "an intention nobody can ever be granted is a rule with no subject");
        resolver.IsAllowed(actors[1], GameIntention.Create).Should().BeTrue();
        resolver.IsAllowed(actors[2], GameIntention.SetStatusModeration).Should().BeTrue();
    }
}
