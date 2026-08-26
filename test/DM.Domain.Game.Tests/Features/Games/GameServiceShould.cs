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
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IGameCreationDataResolver _dataResolver;
    private readonly IGameRepository _repository;
    private IUnreadCountersRepository _unreadCountersRepository = null!;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICache _cache;
    private readonly GameService _service;
    private readonly Guid _currentUserId;
    private readonly DM.Domain.Core.Identity.AuthenticatedUser _author;

    public GameServiceShould()
    {
        var gamesQueryValidator = Mock<IValidator<GamesQuery>>();
        gamesQueryValidator.ValidateAsync(Arg.Any<ValidationContext<GamesQuery>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateGameValidator = Mock<IValidator<UpdateGame>>();
        updateGameValidator.ValidateAsync(Arg.Any<ValidationContext<UpdateGame>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var creationValidator = Mock<IGameCreationValidator>();
        creationValidator.ValidateAndAuthorize(Arg.Any<CreateGame>()).Returns(Task.CompletedTask);

        _dataResolver = Mock<IGameCreationDataResolver>();
        _dataResolver.ResolveTagIds(Arg.Any<IEnumerable<int>?>()).Returns(Array.Empty<Guid>());

        _intentionManager = Mock<IIntentionManager>();

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
        _identityProvider.Current.Returns(identity);

        var gameBlacklistRepository = Mock<IGameBlacklistRepository>();

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        var unreadCountersRepository = _unreadCountersRepository;
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

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);
        _producer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _cache = Mock<ICache>();

        var logger = Mock<ILogger<GameService>>();

        _service = new GameService(
            gamesQueryValidator,
            updateGameValidator,
            creationValidator,
            _dataResolver,
            _intentionManager,
            schemaService,
            _repository,
            userRepository,
            invitationService,
            _identityProvider,
            gameBlacklistRepository,
            unreadCountersRepository,
            subscriptionService,
            _dateTimeProvider,
            _guidFactory,
            _producer,
            _cache,
            logger);
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
        _guidFactory.Create().Returns(gameId, roomId);
        CreateGameEntity? captured = null;
        _repository.Create(
                Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>()).Returns(new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = roomId } } }).AndDoes(ci => { var entity = ci.ArgAt<CreateGameEntity>(0); captured = entity; });

        await _service.CreateAsync(new CreateGame { Title = "Test Game" });

        captured!.PremoderationStatus.Should().Be(expected);
    }

    [Fact]
    public async Task CreateGameAndPublishEvent()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var game = new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = Guid.NewGuid() } } };
        _guidFactory.Create().Returns(gameId, Guid.NewGuid());
        _repository.Create(Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>())
            .Returns(game);

        var result = await _service.CreateAsync(createGame);

        result.Should().NotBeNull();
        await _producer.Received(1).SendAsync(EventType.NewGame, gameId);
    }

    /// <summary>
    /// A game that failed to save leaves no counters behind.
    /// </summary>
    /// <remarks>
    /// The counter write is not part of the game's transaction, so what the
    /// feature owes is an explicit order. Written after the insert, a failed
    /// counter call left a committed game whose unread counters do not exist
    /// and never will — nothing recreates them, and that game's badge reads
    /// zero for everybody forever. Written first, the same failure loses a
    /// game nobody has seen yet.
    /// </remarks>
    [Fact]
    public async Task LeaveNoCountersBehindWhenTheGameItselfFailsToSave()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        _guidFactory.Create().Returns(gameId, roomId);
        _repository.Create(Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("storage refused"));

        var act = async () => await _service.CreateAsync(createGame);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unreadCountersRepository.Received(1).DeleteAsync(roomId, UnreadEntryType.Message);
        await _unreadCountersRepository.Received(1).DeleteAsync(gameId, UnreadEntryType.Message);
        await _unreadCountersRepository.Received(1).DeleteAsync(gameId, UnreadEntryType.Character);
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
        _guidFactory.Create().Returns(gameId, roomId);
        _repository.Create(Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = roomId } } });

        await _service.CreateAsync(createGame);

        await _unreadCountersRepository.Received(1).CreateMarkerAsync(roomId, gameId, UnreadEntryType.Message);
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
        _repository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GameDto?)null);

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
        _repository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);

        var result = await _service.GetAsync(gameId);

        result.Should().BeSameAs(game);
        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Read, game);
    }

    [Fact]
    public async Task AuthorizeUpdateAndAnnounceTheChange()
    {
        var gameId = Guid.NewGuid();
        var updateGame = new UpdateGame { GameId = gameId, Title = "Updated Game" };
        var game = new GameDetails { Id = gameId, Recruitment = new GameRecruitment() };
        _repository.GetGameDetails(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _repository.Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>()).Returns(game);

        var result = await _service.UpdateAsync(updateGame);

        result.Should().BeSameAs(game);
        // The information form of the settings page, so EditSettings and not the
        // lead-only Edit: this is the line the curating mentor passes. Asserting
        // that Edit is never asked keeps the two from being quietly reunited —
        // an extra Edit check here would shut the mentor out again while this
        // test stayed green on the EditSettings half alone.
        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.EditSettings, game);
        _intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.Edit, game);
        // An edit that changes no status announces exactly the one event
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.SequenceEqual(new[] { EventType.ChangedGame })), gameId);
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
        _repository.GetGameDetails(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _repository.Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>()).Returns(game);
        _dataResolver.ResolveTagIds(Arg.Is<IEnumerable<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 3, 7 })))
            .Returns(new[] { fantasy, slowPaced });

        await _service.UpdateAsync(new UpdateGame { GameId = gameId, Tags = new[] { 3, 7 } });

        await _repository.Received(1).Update(
            Arg.Is<UpdateGameEntity>(e => e.TagIds != null && e.TagIds.SequenceEqual(new[] { fantasy, slowPaced })),
            Arg.Any<CancellationToken>());
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
        _repository.GetGameDetails(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _repository.Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>()).Returns(game);

        await _service.UpdateAsync(new UpdateGame
        {
            GameId = gameId,
            Title = "Updated Game",
            Tags = clearing ? Array.Empty<int>() : null
        });

        await _repository.Received(1).Update(
            Arg.Is<UpdateGameEntity>(e => clearing ? e.TagIds != null && !e.TagIds.Any() : e.TagIds == null),
            Arg.Any<CancellationToken>());
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
        _repository.GetGameDetails(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _intentionManager
            .When(m => m.ThrowIfForbidden(GameIntention.EditSettings, game))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав"));

        var act = async () => await _service.UpdateAsync(new UpdateGame { GameId = gameId, Tags = new[] { 3 } });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>());
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
        _repository.GetGameDetails(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _repository.Delete(gameId, _currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.DeleteAsync(gameId);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Delete, game);
        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await _repository.Received(1).Delete(gameId, _currentUserId, Arg.Any<CancellationToken>());
        await _producer.Received(1).SendAsync(EventType.DeletedGame, gameId);
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
        _repository.GetTags(Arg.Any<CancellationToken>()).Returns(tags);
        _cache
            .GetOrCreateAsync(
                Arg.Any<object>(), Arg.Any<Func<Task<IEnumerable<GameTag>>>>(), Arg.Any<TimeSpan>()).Returns(ci => { var create = ci.ArgAt<Func<Task<IEnumerable<GameTag>>>>(1); return create(); });

        var result = await _service.GetTagsAsync();

        result.Should().BeEquivalentTo(tags);
        await _cache.Received(1).GetOrCreateAsync(
            Arg.Any<object>(), Arg.Any<Func<Task<IEnumerable<GameTag>>>>(), CachePolicy.Medium);
    }
}
