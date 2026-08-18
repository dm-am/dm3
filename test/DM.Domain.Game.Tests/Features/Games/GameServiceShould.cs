using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
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

public class GameServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IGameCreationDataResolver> _dataResolver;
    private readonly Mock<IGameRepository> _repository;
    private Mock<IUnreadCountersRepository> _unreadCountersRepository = null!;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<ICache> _cache;
    private readonly GameService _service;
    private readonly Guid _currentUserId;
    private readonly DM.Domain.Core.Identity.AuthenticatedUser _author;

    public GameServiceShould()
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

        _dataResolver = Mock<IGameCreationDataResolver>();
        _dataResolver.Setup(r => r.ResolveTagIds(It.IsAny<IEnumerable<int>?>()))
            .ReturnsAsync(Array.Empty<Guid>());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        var schemaService = Mock<IAttributeSchemaService>();

        _repository = Mock<IGameRepository>();

        var userRepository = Mock<IGameUserRepository>();

        var invitationService = Mock<IGameInvitationService>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        // An established master by default: past the newbie threshold and not
        // watched. The creation-status test moves those two fields and no others.
        var identity = Identities.User(_currentUserId, UserRole.RegularUser);
        _author = identity.User;
        _author.QuantityRating = DM.Domain.Core.Configuration.ProbationPolicy.NewbiePostThreshold;
        _identityProvider.Setup(p => p.Current).Returns(identity);

        var userBlacklistChecker = Mock<DM.Domain.Core.Blacklists.IUserBlacklistChecker>();
        userBlacklistChecker.Setup(c => c.GetBlockedUserIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var gameBlacklistRepository = Mock<IGameBlacklistRepository>();

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        var unreadCountersRepository = _unreadCountersRepository;
        unreadCountersRepository.Setup(r => r.CreateMarkerAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);
        unreadCountersRepository.Setup(r => r.SelectByEntitiesAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync((Guid userId, UnreadEntryType type, Guid[] ids) =>
                ids.ToDictionary(id => id, _ => 0) as IDictionary<Guid, int>);

        var subscriptionService = Mock<IGameSubscriptionService>();

        var roomRepository = Mock<IRoomRepository>();

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        var intentionConverter = Mock<IGameIntentionConverter>();
        intentionConverter.Setup(c => c.Convert(It.IsAny<ModuleStatus>()))
            .Returns((GameIntention.Edit, EventType.ChangedGame));

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _producer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _cache = Mock<ICache>();

        var logger = Mock<ILogger<GameService>>();

        _service = new GameService(
            gamesQueryValidator.Object,
            updateGameValidator.Object,
            creationValidator.Object,
            _dataResolver.Object,
            _intentionManager.Object,
            schemaService.Object,
            _repository.Object,
            userRepository.Object,
            invitationService.Object,
            _identityProvider.Object,
            userBlacklistChecker.Object,
            gameBlacklistRepository.Object,
            unreadCountersRepository.Object,
            subscriptionService.Object,
            roomRepository.Object,
            _dateTimeProvider.Object,
            _guidFactory.Object,
            intentionConverter.Object,
            _producer.Object,
            _cache.Object,
            logger.Object);
    }

    /// <summary>
    /// The premoderation status a game is born in, over all four combinations of
    /// the two things that hold an author back.
    /// </summary>
    /// <remarks>
    /// Nothing wrote this field before, so every game on the site was created
    /// Approved and premoderation applied to nobody. The rule itself lives in
    /// ModulePremoderationPolicy and is shared with the blog; what is proved here
    /// is that the creation path asks it.
    /// </remarks>
    [Theory]
    [InlineData(false, false, PremoderationStatus.Approved)]
    [InlineData(true, false, PremoderationStatus.AwaitingEdits)]
    [InlineData(false, true, PremoderationStatus.AwaitingEdits)]
    [InlineData(true, true, PremoderationStatus.AwaitingEdits)]
    public async Task CreateAGameInTheStatusItsMasterEarns(
        bool newbie, bool underWatch, PremoderationStatus expected)
    {
        _author.QuantityRating = newbie ? 0 : DM.Domain.Core.Configuration.ProbationPolicy.NewbiePostThreshold;
        _author.IsUnderModerationWatch = underWatch;

        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        _guidFactory.SetupSequence(g => g.Create()).Returns(gameId).Returns(roomId);
        CreateGameEntity? captured = null;
        _repository.Setup(r => r.Create(
                It.IsAny<CreateGameEntity>(), It.IsAny<CreateRoomEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateGameEntity, CreateRoomEntity, CancellationToken>((entity, _, _) => captured = entity)
            .ReturnsAsync(new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = roomId } } });

        await _service.CreateAsync(new CreateGame { Title = "Test Game" });

        captured!.PremoderationStatus.Should().Be(expected);
    }

    [Fact]
    public async Task CreateGameAndPublishEvent()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var game = new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = Guid.NewGuid() } } };
        _guidFactory.SetupSequence(g => g.Create())
            .Returns(gameId)
            .Returns(Guid.NewGuid());
        _repository.Setup(r => r.Create(It.IsAny<CreateGameEntity>(), It.IsAny<CreateRoomEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var result = await _service.CreateAsync(createGame);

        result.Should().NotBeNull();
        _producer.Verify(p => p.SendAsync(EventType.NewGame, gameId), Times.Once);
    }

    /// <summary>
    /// A game that failed to save leaves no counters behind.
    /// </summary>
    /// <remarks>
    /// There is no transaction across PostgreSQL and MongoDB and no outbox, so
    /// what a feature living in two stores owes is an explicit order. Written
    /// after the insert, a failed Mongo call left a committed game whose unread
    /// counters do not exist and never will — nothing recreates them, and that
    /// game's badge reads zero for everybody forever. Written first, the same
    /// failure loses a game nobody has seen yet.
    /// </remarks>
    [Fact]
    public async Task LeaveNoCountersBehindWhenTheGameItselfFailsToSave()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        _guidFactory.SetupSequence(g => g.Create()).Returns(gameId).Returns(roomId);
        _repository.Setup(r => r.Create(It.IsAny<CreateGameEntity>(), It.IsAny<CreateRoomEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage refused"));

        var act = async () => await _service.CreateAsync(createGame);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unreadCountersRepository.Verify(r => r.DeleteAsync(roomId, UnreadEntryType.Message), Times.Once);
        _unreadCountersRepository.Verify(r => r.DeleteAsync(gameId, UnreadEntryType.Message), Times.Once);
        _unreadCountersRepository.Verify(r => r.DeleteAsync(gameId, UnreadEntryType.Character), Times.Once);
    }

    /// <summary>
    /// The first room of a game is parented by that game, like every room made
    /// after it.
    /// </summary>
    [Fact]
    public async Task ParentTheFirstRoomsCounterByItsGame()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        _guidFactory.SetupSequence(g => g.Create()).Returns(gameId).Returns(roomId);
        _repository.Setup(r => r.Create(It.IsAny<CreateGameEntity>(), It.IsAny<CreateRoomEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = roomId } } });

        await _service.CreateAsync(createGame);

        _unreadCountersRepository.Verify(
            r => r.CreateMarkerAsync(roomId, gameId, UnreadEntryType.Message), Times.Once);
    }

    // A committed game not turned into a 500 by the announcement of it used to be
    // asserted here, against a producer mocked to throw. The guard moved into the
    // producer itself — see EventPublishingShould — because it was owed by all
    // seventy-odd publishing call sites and written at one. A mock that throws now
    // contradicts every implementation of the interface.

    [Fact]
    public async Task ThrowNotFoundWhenGameDoesNotExist()
    {
        var gameId = Guid.NewGuid();
        _repository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((GameDto?)null);

        var act = async () => await _service.GetAsync(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeAndReturnTheGameOnRead()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };
        _repository.Setup(r => r.GetGame(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _service.GetAsync(gameId);

        result.Should().BeSameAs(game);
        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Read, game), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateAndAnnounceTheChange()
    {
        var gameId = Guid.NewGuid();
        var updateGame = new UpdateGame { GameId = gameId, Title = "Updated Game" };
        var game = new GameDetails { Id = gameId, Recruitment = new GameRecruitment() };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _repository.Setup(r => r.Update(It.IsAny<UpdateGameEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var result = await _service.UpdateAsync(updateGame);

        result.Should().BeSameAs(game);
        // The information form of the settings page, so EditSettings and not the
        // lead-only Edit: this is the line the curating mentor passes. Asserting
        // that Edit is never asked keeps the two from being quietly reunited —
        // an extra Edit check here would shut the mentor out again while this
        // test stayed green on the EditSettings half alone.
        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.EditSettings, game), Times.Once);
        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Edit, game), Times.Never);
        // An edit that changes no status announces exactly the one event
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.SequenceEqual(new[] { EventType.ChangedGame })), gameId), Times.Once);
    }

    /// <summary>
    /// Tags are editable after creation, and the settings page is where they are
    /// edited. The wire carries short identifiers — what the tag list serves and
    /// what the game filters take — so the update goes through the same catalog
    /// translation the creation form does; the link table keys on the tags' own
    /// identifiers and nothing else may reach it.
    /// </summary>
    [Fact]
    public async Task TranslateTagShortIdsOnUpdateBeforeWritingThem()
    {
        var gameId = Guid.NewGuid();
        var fantasy = Guid.NewGuid();
        var slowPaced = Guid.NewGuid();
        var game = new GameDetails { Id = gameId, Recruitment = new GameRecruitment() };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _repository.Setup(r => r.Update(It.IsAny<UpdateGameEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _dataResolver.Setup(r => r.ResolveTagIds(It.Is<IEnumerable<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 3, 7 }))))
            .ReturnsAsync(new[] { fantasy, slowPaced });

        await _service.UpdateAsync(new UpdateGame { GameId = gameId, Tags = new[] { 3, 7 } });

        _repository.Verify(r => r.Update(
            It.Is<UpdateGameEntity>(e => e.TagIds != null && e.TagIds.SequenceEqual(new[] { fantasy, slowPaced })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Null and empty are two different answers, and the repository writes them
    /// differently: an update that says nothing about tags must leave them
    /// standing, while an empty list is the master taking every tag off. Saving
    /// the title alone would otherwise strip the game of its tags.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TellTagsLeftAloneApartFromTagsClearedOnUpdate(bool clearing)
    {
        var gameId = Guid.NewGuid();
        var game = new GameDetails { Id = gameId, Recruitment = new GameRecruitment() };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _repository.Setup(r => r.Update(It.IsAny<UpdateGameEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await _service.UpdateAsync(new UpdateGame
        {
            GameId = gameId,
            Title = "Updated Game",
            Tags = clearing ? Array.Empty<int>() : null
        });

        _repository.Verify(r => r.Update(
            It.Is<UpdateGameEntity>(e => clearing ? e.TagIds != null && !e.TagIds.Any() : e.TagIds == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tags answer to the same gate as the rest of the information form, so a
    /// stranger cannot retag someone else's game: the refusal arrives before
    /// anything is written, and nothing reaches the repository.
    /// </summary>
    [Fact]
    public async Task RefuseATagChangeFromSomeoneWhoMayNotEditTheSettings()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDetails { Id = gameId, Recruitment = new GameRecruitment() };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _intentionManager
            .Setup(m => m.ThrowIfForbidden(GameIntention.EditSettings, game))
            .Throws(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав"));

        var act = async () => await _service.UpdateAsync(new UpdateGame { GameId = gameId, Tags = new[] { 3 } });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        _repository.Verify(r => r.Update(It.IsAny<UpdateGameEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthorizeDeleteAndAnnounceIt()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDetails
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            Recruitment = new GameRecruitment()
        };
        _repository.Setup(r => r.GetGameDetails(gameId, _currentUserId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _repository.Setup(r => r.Delete(gameId, _currentUserId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _service.DeleteAsync(gameId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Delete, game), Times.Once);
        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        _repository.Verify(r => r.Delete(gameId, _currentUserId, It.IsAny<CancellationToken>()), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.DeletedGame, gameId), Times.Once);
    }

    /// <summary>
    /// The tag list is not the tag catalog alone: every entry carries the number
    /// of active games with that tag, so a day-long entry shows the filter the
    /// counts of yesterday.
    /// </summary>
    [Fact]
    public async Task CacheTheTagListOnlyAsLongAsItsGameCountsHold()
    {
        var tags = new[]
        {
            new GameTag
            {
                Id = Guid.NewGuid(), ShortId = 1, Title = "Fantasy", GroupTitle = "Setting", GamesCount = 3
            }
        };
        _repository.Setup(r => r.GetTags(It.IsAny<CancellationToken>())).ReturnsAsync(tags);
        _cache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<object>(), It.IsAny<Func<Task<IEnumerable<GameTag>>>>(), It.IsAny<TimeSpan>()))
            .Returns((object _, Func<Task<IEnumerable<GameTag>>> create, TimeSpan _) => create());

        var result = await _service.GetTagsAsync();

        result.Should().BeEquivalentTo(tags);
        _cache.Verify(c => c.GetOrCreateAsync(
            It.IsAny<object>(), It.IsAny<Func<Task<IEnumerable<GameTag>>>>(), CachePolicy.Medium), Times.Once);
    }
}
