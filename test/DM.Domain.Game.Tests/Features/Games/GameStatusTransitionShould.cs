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
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameStatusTransitionShould : UnitTestBase
{
    private readonly Mock<IGameRepository> _repository;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly GameService _service;
    private readonly Guid _currentUserId;
    private readonly DateTimeOffset _now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private UpdateGameEntity? _capturedUpdate;

    public GameStatusTransitionShould()
    {
        var gamesQueryValidator = Mock<IValidator<GamesQuery>>();
        gamesQueryValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<GamesQuery>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateGameValidator = Mock<IValidator<UpdateGame>>();
        updateGameValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateGame>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var creationValidator = Mock<IGameCreationValidator>();
        creationValidator.Setup(v => v.ValidateAndAuthorize(It.IsAny<CreateGame>()))
            .Returns(Task.CompletedTask);

        var dataResolver = Mock<IGameCreationDataResolver>();
        dataResolver.Setup(r => r.ResolveTagIds(It.IsAny<IEnumerable<int>?>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var intentionManager = Mock<IIntentionManager>();
        intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>()));
        intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        var schemaService = Mock<IAttributeSchemaService>();

        _repository = Mock<IGameRepository>();

        var userRepository = Mock<IGameUserRepository>();

        var invitationService = Mock<IGameInvitationService>();

        _currentUserId = Guid.NewGuid();
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        var userBlacklistChecker = Mock<DM.Domain.Core.Blacklists.IUserBlacklistChecker>();
        userBlacklistChecker.Setup(c => c.GetBlockedUserIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var gameBlacklistRepository = Mock<IGameBlacklistRepository>();

        var unreadCountersRepository = Mock<IUnreadCountersRepository>();
        unreadCountersRepository.Setup(r => r.CreateAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);
        unreadCountersRepository.Setup(r => r.SelectByEntitiesAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync((Guid userId, UnreadEntryType type, Guid[] ids) =>
                ids.ToDictionary(id => id, _ => 0) as IDictionary<Guid, int>);

        var subscriptionService = Mock<IGameSubscriptionService>();
        subscriptionService.Setup(s => s.GetSubscribersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DM.Domain.Core.Dto.UserReference>());

        var roomRepository = Mock<IRoomRepository>();

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        var intentionConverter = Mock<IGameIntentionConverter>();
        intentionConverter.Setup(c => c.Convert(It.IsAny<ModuleStatus>()))
            .Returns((GameIntention.Edit, EventType.ChangedGame));

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _producer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        var cache = Mock<ICache>();

        var logger = Mock<ILogger<GameService>>();

        _service = new GameService(
            gamesQueryValidator.Object,
            updateGameValidator.Object,
            creationValidator.Object,
            dataResolver.Object,
            intentionManager.Object,
            schemaService.Object,
            _repository.Object,
            userRepository.Object,
            invitationService.Object,
            identityProvider.Object,
            userBlacklistChecker.Object,
            gameBlacklistRepository.Object,
            unreadCountersRepository.Object,
            subscriptionService.Object,
            roomRepository.Object,
            _dateTimeProvider.Object,
            guidFactory.Object,
            intentionConverter.Object,
            _producer.Object,
            cache.Object,
            logger.Object);
    }

    private Guid SetupGame(
        ModuleStatus status,
        ClosedReason closedReason = ClosedReason.None,
        DateTimeOffset? activatedUtc = null,
        DateTimeOffset? closedUtc = null,
        PremoderationStatus premoderationStatus = PremoderationStatus.Approved)
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
            Recruitment = new GameRecruitment()
        };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _repository.Setup(r => r.Update(It.IsAny<UpdateGameEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateGameEntity, CancellationToken>((update, _) => _capturedUpdate = update)
            .ReturnsAsync(game);
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
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameActive)), gameId), Times.Once);
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
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameFrozen)), gameId), Times.Once);
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
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameFinished)), gameId), Times.Once);
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
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameClosed)), gameId), Times.Once);
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
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameActive)), gameId), Times.Once);
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

    [Fact]
    public async Task SendGameToPremoderationAndAssignCurator()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(gameId.ToString(), ModulePremoderationTransition.SendToPremoderation);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingApproval);
        _capturedUpdate.MentorId.Should().Be(_currentUserId);
        _capturedUpdate.SetMentorId.Should().BeTrue();
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameModeration)), gameId), Times.Once);
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    public async Task RejectSendToPremoderationWhenNotAwaitingEdits(PremoderationStatus premoderationStatus)
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: premoderationStatus);

        var act = async () => await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SendToPremoderation);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveGameFromPremoderationAndClearCurator()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingApproval);

        await _service.ChangePremoderationAsync(gameId.ToString(), ModulePremoderationTransition.RemoveFromPremoderation);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.Approved);
        _capturedUpdate.MentorId.Should().BeNull();
        _capturedUpdate.SetMentorId.Should().BeTrue();
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameModeration)), gameId), Times.Once);
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task RejectRemoveFromPremoderationWhenNotAwaitingApproval(PremoderationStatus premoderationStatus)
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: premoderationStatus);

        var act = async () => await _service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.RemoveFromPremoderation);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion
}
