using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameServiceShould : GameServiceTestBase
{
    public GameServiceShould()
    {
        // An established master by default: past the newbie threshold and not
        // watched. The creation-status test moves those two fields and no others.
        author.QuantityRating = ProbationPolicy.NewbiePostThreshold;
    }

    /// <summary>
    /// The premoderation status a game is born in, over all four combinations of
    /// the two things that hold an author back.
    /// </summary>
    /// <remarks>
    /// The rule lives in ModulePremoderationPolicy, what is proved here is that the creation path asks it.
    /// </remarks>
    [Theory]
    [InlineData(false, false, PremoderationStatus.Approved)]
    [InlineData(true, false, PremoderationStatus.AwaitingEdits)]
    [InlineData(false, true, PremoderationStatus.AwaitingEdits)]
    [InlineData(true, true, PremoderationStatus.AwaitingEdits)]
    public async Task CreateAGameInTheStatusItsMasterEarns(
        bool newbie, bool underWatch, PremoderationStatus expected)
    {
        author.QuantityRating = newbie ? 0 : ProbationPolicy.NewbiePostThreshold;
        author.IsUnderModerationWatch = underWatch;

        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        guidFactory.Create().Returns(gameId, roomId);
        CreateGameEntity? captured = null;
        repository.Create(
                Arg.Do<CreateGameEntity>(e => captured = e), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = roomId } } });

        await service.CreateAsync(new CreateGame { Title = "Test Game" });

        captured!.PremoderationStatus.Should().Be(expected);
    }

    [Fact]
    public async Task CreateGameAndPublishEvent()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var game = new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = Guid.NewGuid() } } };
        guidFactory.Create().Returns(gameId, Guid.NewGuid());
        repository.Create(Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>())
            .Returns(game);

        await service.CreateAsync(createGame);

        await producer.Received(1).SendAsync(EventType.NewGame, gameId);
    }

    /// <remarks>
    /// The counters are outside the game's transaction, and written first a failure loses a game nobody has seen.
    /// </remarks>
    [Fact]
    public async Task LeaveNoCountersBehindWhenTheGameItselfFailsToSave()
    {
        var createGame = new CreateGame { Title = "Test Game", SystemName = "Test System" };
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        guidFactory.Create().Returns(gameId, roomId);
        repository.Create(Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("storage refused"));

        var act = async () => await service.CreateAsync(createGame);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await unreadCountersRepository.Received(1).DeleteAsync(roomId, UnreadEntryType.Message);
        await unreadCountersRepository.Received(1).DeleteAsync(gameId, UnreadEntryType.Message);
        await unreadCountersRepository.Received(1).DeleteAsync(gameId, UnreadEntryType.Character);
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
        guidFactory.Create().Returns(gameId, roomId);
        repository.Create(Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameDetails { Id = gameId, Rooms = new[] { new Room { Id = roomId } } });

        await service.CreateAsync(createGame);

        await unreadCountersRepository.Received(1).CreateMarkerAsync(roomId, gameId, UnreadEntryType.Message);
    }

    [Fact]
    public async Task ThrowNotFoundWhenGameDoesNotExist()
    {
        var gameId = Guid.NewGuid();
        repository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((GameDto?)null);

        var act = async () => await service.GetAsync(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeAndReturnTheGameOnRead()
    {
        var gameId = Guid.NewGuid();
        var game = GameBuilder.Bare(gameId);
        repository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);

        var result = await service.GetAsync(gameId);

        result.Should().BeSameAs(game);
        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Read, game);
    }

    [Fact]
    public async Task AuthorizeUpdateAndAnnounceTheChange()
    {
        var gameId = Guid.NewGuid();
        var updateGame = new UpdateGame { GameId = gameId, Title = "Updated Game" };
        var game = new GameDetails { Id = gameId, Recruitment = new GameRecruitment() };
        repository.GetGameDetails(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        repository.Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>()).Returns(game);

        var result = await service.UpdateAsync(updateGame);

        result.Should().BeSameAs(game);
        // The information form of the settings page, so EditSettings and not the
        // lead-only Edit: this is the line the curating mentor passes. Asserting
        // that Edit is never asked keeps the two from being quietly reunited —
        // an extra Edit check here would shut the mentor out again while this
        // test stayed green on the EditSettings half alone.
        intentionManager.Received(1).ThrowIfForbidden(GameIntention.EditSettings, game);
        intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.Edit, game);
        // An edit that changes no status announces exactly the one event
        await producer.Received(1).SendAsync(
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
        repository.GetGameDetails(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        repository.Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>()).Returns(game);
        tagResolver.ResolveTagIds(Arg.Is<IEnumerable<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 3, 7 })))
            .Returns(new[] { fantasy, slowPaced });

        await service.UpdateAsync(new UpdateGame { GameId = gameId, Tags = new[] { 3, 7 } });

        await repository.Received(1).Update(
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
        repository.GetGameDetails(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        repository.Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>()).Returns(game);

        await service.UpdateAsync(new UpdateGame
        {
            GameId = gameId,
            Title = "Updated Game",
            Tags = clearing ? Array.Empty<int>() : null
        });

        await repository.Received(1).Update(
            Arg.Is<UpdateGameEntity>(e => clearing ? e.TagIds != null && !e.TagIds.Any() : e.TagIds == null),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Creation answers to the create intention right after the form passes
    /// validation: someone the policy refuses gets the refusal before anything
    /// is written, and no game reaches the repository.
    /// </summary>
    [Fact]
    public async Task RefuseToCreateAGameForSomeoneWhoMayNotCreateOne()
    {
        intentionManager
            .When(m => m.ThrowIfForbidden(GameIntention.Create))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав"));

        var act = async () => await service.CreateAsync(new CreateGame { Title = "Test Game" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Create(Arg.Any<CreateGameEntity>(), Arg.Any<CreateRoomEntity>(), Arg.Any<CancellationToken>());
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
        repository.GetGameDetails(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        intentionManager
            .When(m => m.ThrowIfForbidden(GameIntention.EditSettings, game))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав"));

        var act = async () => await service.UpdateAsync(new UpdateGame { GameId = gameId, Tags = new[] { 3 } });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Update(Arg.Any<UpdateGameEntity>(), Arg.Any<CancellationToken>());
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
        repository.GetGameDetails(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        repository.Delete(gameId, currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await service.DeleteAsync(gameId);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Delete, game);
        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await repository.Received(1).Delete(gameId, currentUserId, Arg.Any<CancellationToken>());
        await producer.Received(1).SendAsync(EventType.DeletedGame, gameId);
    }

    /// <summary>
    /// The master removes someone who is not an assistant of the game: the refusal says so
    /// and calls the person an assistant.
    /// </summary>
    /// <remarks>
    /// The assistant repository substitute answers false to "is an assistant": nobody is.
    /// </remarks>
    [Fact]
    public async Task RefuseToRemoveSomeoneWhoIsNotAnAssistantOfTheGame()
    {
        var gameId = Guid.NewGuid();
        var game = GameBuilder.Bare(gameId);
        repository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);

        var act = async () => await service.RemoveAssistantAsync(gameId, "stranger");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == "Ассистент не найден в этой игре");
        await producer.DidNotReceive().SendAsync(EventType.ChangedGame, gameId);
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
        repository.GetTags(Arg.Any<CancellationToken>()).Returns(tags);
        cache
            .GetOrCreateAsync(
                Arg.Any<object>(), Arg.Any<Func<Task<IEnumerable<GameTag>>>>(), Arg.Any<TimeSpan>())
            .Returns(ci => ci.ArgAt<Func<Task<IEnumerable<GameTag>>>>(1)());

        var result = await service.GetTagsAsync();

        result.Should().BeEquivalentTo(tags);
        await cache.Received(1).GetOrCreateAsync(
            Arg.Any<object>(), Arg.Any<Func<Task<IEnumerable<GameTag>>>>(), CachePolicy.Medium);
    }
}
