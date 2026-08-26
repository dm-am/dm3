using System;
using System.Linq;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Testing.Dsl;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using AwesomeAssertions;
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

    /// <summary>
    /// A premoderation verdict is a rank and nothing else: the mentor and
    /// everybody above one in moderation, whatever their relationship to the game.
    /// </summary>
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

    /// <summary>
    /// Whoever may pass the verdict may open the game it is pending on, holding
    /// no role in that game whatsoever.
    /// </summary>
    /// <remarks>
    /// The curator arm cannot cover this: a game in AwaitingEdits records no
    /// curator, and that is the status every newbie's game is created in. Until
    /// the rank arm existed the read gate stopped at senior moderation, so a
    /// mentor and a moderator were handed a queue whose every entry refused them.
    /// </remarks>
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

    /// <summary>
    /// A player of somebody else's game is a stranger to this one: the rank arm
    /// is a rank and the role arms are about this game, and neither of them is a
    /// claim about games the reader plays elsewhere.
    /// </summary>
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

    /// <summary>
    /// The rank opens games awaiting a verdict and nothing else. A private draft
    /// is hidden by its author's choice, not by premoderation, and no mentor is
    /// owed a look at it.
    /// </summary>
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

    /// <summary>
    /// The rank admits a mentor to a premoderated game somebody else curates, and
    /// to one nobody curates at all.
    /// </summary>
    /// <remarks>
    /// This used to be the opposite assertion — the assignment admitted, the rank
    /// did not — and it could not hold once the verdict became a site-wide move
    /// legal from every status: a game in AwaitingEdits records no curator, so
    /// under the old rule the queue's own entries refused the mentor reading them.
    /// What the rank still does not open is a game hidden by something other than
    /// premoderation; that half is asserted just below.
    /// </remarks>
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

    [Fact]
    public void AllowEditSettingsForMaster()
    {
        var masterId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithMaster(masterId)
            .Please();
        var user = Create.User(masterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.EditSettings, game).Should().BeTrue();
    }

    [Fact]
    public void AllowEditSettingsForAssistant()
    {
        var assistantId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithAssistants(assistantId)
            .Please();
        var user = Create.User(assistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GameIntention.EditSettings, game).Should().BeTrue();
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
    /// When this gate was written, nearly half the members were named in no test
    /// at all. This is the gate that keeps the count from sliding back: an arm
    /// that stops granting anything - the shape a careless simplification takes -
    /// and a member added with no rule behind it both land here. The seats and
    /// states below are the ones the arms are written for; add a member and this
    /// is red until its case joins them.
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
