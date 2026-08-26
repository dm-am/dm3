using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Statuses;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameStatusTransitionShould : UnitTestBase
{
    private readonly IGameRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IEventProducer _producer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly GameService _service;
    private readonly Guid _currentUserId;
    private readonly DateTimeOffset _now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private UpdateGameEntity? _capturedUpdate;

    public GameStatusTransitionShould()
    {
        var gamesQueryValidator = Mock<IValidator<GamesQuery>>();
        gamesQueryValidator.ValidateAsync(Arg.Any<ValidationContext<GamesQuery>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateGameValidator = Mock<IValidator<UpdateGame>>();
        updateGameValidator.ValidateAsync(Arg.Any<ValidationContext<UpdateGame>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var creationValidator = Mock<IGameCreationValidator>();
        creationValidator.ValidateAndAuthorize(Arg.Any<CreateGame>()).Returns(Task.CompletedTask);

        var dataResolver = Mock<IGameCreationDataResolver>();
        dataResolver.ResolveTagIds(Arg.Any<IEnumerable<int>?>()).Returns(Array.Empty<Guid>());

        _intentionManager = Mock<IIntentionManager>();

        var schemaService = Mock<IAttributeSchemaService>();

        _repository = Mock<IGameRepository>();

        var userRepository = Mock<IGameUserRepository>();

        var invitationService = Mock<IGameInvitationService>();

        _currentUserId = Guid.NewGuid();
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        var gameBlacklistRepository = Mock<IGameBlacklistRepository>();

        var unreadCountersRepository = Mock<IUnreadCountersRepository>();
        unreadCountersRepository.CreateMarkerAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>())
            .Returns(Task.CompletedTask);
        unreadCountersRepository.SelectByEntitiesAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid[]>())
            .Returns(ci =>
            {
                var userId = ci.ArgAt<Guid>(0);
                var type = ci.ArgAt<UnreadEntryType>(1);
                var ids = ci.ArgAt<Guid[]>(2);
                return ids.ToDictionary(id => id, _ => 0) as IDictionary<Guid, int>;
            });

        var subscriptionService = Mock<IGameSubscriptionService>();
        subscriptionService.GetSubscribersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DM.Domain.Core.Dto.UserReference>());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(_now);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);
        _producer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        var cache = Mock<ICache>();

        var logger = Mock<ILogger<GameService>>();

        _service = new GameService(
            gamesQueryValidator,
            updateGameValidator,
            creationValidator,
            dataResolver,
            _intentionManager,
            schemaService,
            _repository,
            userRepository,
            invitationService,
            identityProvider,
            gameBlacklistRepository,
            unreadCountersRepository,
            subscriptionService,
            _dateTimeProvider,
            guidFactory,
            _producer,
            cache,
            logger);
    }

    private Guid SetupGame(
        ModuleStatus status,
        ClosedReason closedReason = ClosedReason.None,
        DateTimeOffset? activatedUtc = null,
        DateTimeOffset? closedUtc = null,
        PremoderationStatus premoderationStatus = PremoderationStatus.Approved,
        Guid? masterId = null)
    {
        var gameId = Guid.NewGuid();
        var game = new GameDetails
        {
            Id = gameId,
            Status = status,
            ClosedReason = closedReason,
            ActivatedUtc = activatedUtc,
            ClosedUtc = closedUtc,
            PremoderationStatus = premoderationStatus,
            Master = new DM.Domain.Core.Dto.GeneralUser { UserId = masterId ?? _currentUserId },
            Recruitment = new GameRecruitment()
        };
        _repository.GetGameDetails(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        // The mentor's two moves read past the accessibility scope, because a game
        // awaiting edits has no curator and the scope would hide it from them.
        _repository.GetGameDetailsForModeration(
                gameId, _currentUserId, Arg.Any<CancellationToken>()).Returns(game);
        _repository.Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>())
            .Returns(game)
            .AndDoes(ci =>
            {
                var update = ci.ArgAt<UpdateGameEntity>(0);
                _capturedUpdate = update;
            });
        return gameId;
    }

    #region Start

    [Fact]
    public async Task StartDraftGameAndSetActivatedUtcOnFirstActivation()
    {
        var gameId = SetupGame(ModuleStatus.Draft);

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Start);

        _capturedUpdate.Should().NotBeNull();
        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _capturedUpdate.ActivatedUtc.Should().Be(_now);
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameActive)), gameId);
    }

    [Fact]
    public async Task StartDraftGameWithoutOverwritingActivatedUtc()
    {
        var firstActivation = _now.AddMonths(-1);
        var gameId = SetupGame(ModuleStatus.Draft, activatedUtc: firstActivation);

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Start);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _capturedUpdate.ActivatedUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(ModuleStatus.Active)]
    [InlineData(ModuleStatus.Closed)]
    public async Task RejectStartFromNonDraftStatus(ModuleStatus status)
    {
        var gameId = SetupGame(status);

        var act = async () => await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Freeze

    [Fact]
    public async Task FreezeActiveGameWithFrozenReason()
    {
        var gameId = SetupGame(ModuleStatus.Active);

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Freeze);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.Frozen);
        _capturedUpdate.ClosedUtc.Should().Be(_now);
        _capturedUpdate.IsRecruitmentOpen.Should().BeFalse();
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameFrozen)), gameId);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Closed)]
    public async Task RejectFreezeFromNonActiveStatus(ModuleStatus status)
    {
        var gameId = SetupGame(status);

        var act = async () => await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Freeze);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Finish

    [Fact]
    public async Task FinishActiveGameWithFinishedReason()
    {
        var gameId = SetupGame(ModuleStatus.Active);

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Finish);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.Finished);
        _capturedUpdate.ClosedUtc.Should().Be(_now);
        _capturedUpdate.IsRecruitmentOpen.Should().BeFalse();
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameFinished)), gameId);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Closed)]
    public async Task RejectFinishFromNonActiveStatus(ModuleStatus status)
    {
        var gameId = SetupGame(status);

        var act = async () => await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Finish);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Close

    [Fact]
    public async Task CloseActiveGameWithNoneReasonAndSetClosedUtc()
    {
        var gameId = SetupGame(ModuleStatus.Active);

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Close);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        _capturedUpdate.ClosedUtc.Should().Be(_now);
        _capturedUpdate.IsRecruitmentOpen.Should().BeFalse();
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameClosed)), gameId);
    }

    [Fact]
    public async Task CloseFrozenGameWithoutOverwritingClosedUtc()
    {
        var frozenAt = _now.AddDays(-7);
        var gameId = SetupGame(ModuleStatus.Closed, ClosedReason.Frozen, closedUtc: frozenAt);

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Close);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        _capturedUpdate.ClosedUtc.Should().BeNull();
    }

    [Fact]
    public async Task RejectCloseOfFinishedGame()
    {
        var gameId = SetupGame(ModuleStatus.Closed, ClosedReason.Finished, closedUtc: _now.AddDays(-7));

        var act = async () => await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Close);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectCloseOfDraftGame()
    {
        var gameId = SetupGame(ModuleStatus.Draft);

        var act = async () => await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Close);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Reopen

    [Theory]
    [InlineData(ClosedReason.None)]
    [InlineData(ClosedReason.Finished)]
    [InlineData(ClosedReason.Frozen)]
    public async Task ReopenClosedGameFromAnyReason(ClosedReason closedReason)
    {
        var gameId = SetupGame(ModuleStatus.Closed, closedReason,
            activatedUtc: _now.AddMonths(-2), closedUtc: _now.AddDays(-7));

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Reopen);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        _capturedUpdate.ClearClosedUtc.Should().BeTrue();
        _capturedUpdate.ActivatedUtc.Should().BeNull(); // Already activated before
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameActive)), gameId);
    }

    [Fact]
    public async Task ReopenNeverActivatedGameAndSetActivatedUtc()
    {
        var gameId = SetupGame(ModuleStatus.Closed, closedUtc: _now.AddDays(-7));

        await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Reopen);

        _capturedUpdate!.ActivatedUtc.Should().Be(_now);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Active)]
    public async Task RejectReopenFromNonClosedStatus(ModuleStatus status)
    {
        var gameId = SetupGame(status);

        var act = async () => await _service.ChangeStatusAsync(gameId, ModuleStatusTransition.Reopen);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Premoderation

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task ApproveAGameFromAnyStatusAndClearTheCurator(PremoderationStatus current)
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: current);

        await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SetApproved);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.Approved);
        _capturedUpdate.MentorId.Should().BeNull();
        _capturedUpdate.SetMentorId.Should().BeTrue();
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameModeration)), gameId);
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task ReturnAGameForEditsFromAnyStatusAndRecordTheCurator(PremoderationStatus current)
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: current);

        await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SetAwaitingEdits);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingEdits);
        _capturedUpdate.MentorId.Should().Be(_currentUserId);
        _capturedUpdate.SetMentorId.Should().BeTrue();
    }

    /// <summary>
    /// The two verdicts are a rank and nothing else, so they are asked of the
    /// targetless intention — before the game is read, which is what keeps a
    /// stranger probing aliases from learning whether one resolves.
    /// </summary>
    [Theory]
    [InlineData(ModulePremoderationTransition.SetApproved)]
    [InlineData(ModulePremoderationTransition.SetAwaitingEdits)]
    public async Task AskTheMentorRankForAVerdictAndNothingAboutTheGame(
        ModulePremoderationTransition transition)
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(gameId.ToString(), transition);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.SetStatusModeration);
        _intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.SubmitForApproval, Arg.Any<GameDetails>());
    }

    [Fact]
    public async Task SubmitAGameAwaitingEditsForApprovalWithoutTouchingTheCurator()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingApproval);
        _capturedUpdate.SetMentorId.Should().BeFalse();
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameModeration)), gameId);
    }

    /// <summary>
    /// The author's move is a fact about the game, so it is asked of the targeted
    /// intention — and the mentor rank is not asked at all, because the master
    /// making this move normally does not hold it.
    /// </summary>
    [Fact]
    public async Task AskTheGameWhoTheAuthorIsAndNotTheMentorRank()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.SubmitForApproval, Arg.Any<GameDetails>());
        _intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.SetStatusModeration);
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    public async Task RejectSubmitForApprovalWhenNotAwaitingEdits(PremoderationStatus current)
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: current);

        var act = async () => await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Illegality is answered before authorization, as on the status machine: the
    /// author gets a 400 naming the move, not a 403 about who they are.
    /// </summary>
    [Fact]
    public async Task RefuseAnIllegalSubmitBeforeAskingWhoTheCallerIs()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.Approved);

        var act = async () => await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        await act.Should().ThrowAsync<HttpException>();
        _intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.SubmitForApproval, Arg.Any<GameDetails>());
    }

    #endregion
}
