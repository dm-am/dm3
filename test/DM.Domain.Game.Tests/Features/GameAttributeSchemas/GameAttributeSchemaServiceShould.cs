using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentValidation;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Features.GameAttributeSchemas;

/// <summary>
/// The order the refusals of a replacement come in, and what each of them
/// leaves untouched.
/// </summary>
/// <remarks>
/// The order is the point. A body the server could not store is refused before
/// any game is looked up, so an unstorable request tells a stranger nothing
/// about which games exist; the game is then resolved by the same read that
/// serves its details, so an invisible one answers the same way it answers a
/// reader; and only a caller who got that far is measured against the version.
/// </remarks>
public class GameAttributeSchemaServiceShould : UnitTestBase
{
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameAttributeSchemaRepository _repository;
    private readonly GameAttributeSchemaService _service;

    private readonly Guid _gameId = Guid.NewGuid();

    public GameAttributeSchemaServiceShould()
    {
        _gameService = Mock<IGameService>();
        _gameService.GetAsync(_gameId).Returns(new GameDto { Id = _gameId });

        _intentionManager = Mock<IIntentionManager>();
        _repository = Mock<IGameAttributeSchemaRepository>();
        _repository.Replace(Arg.Any<ReplaceGameAttributeSchema>())
            .Returns(new GameAttributeSchema { Version = 1 });

        _service = new GameAttributeSchemaService(
            new ReplaceGameAttributeSchemaValidator(),
            _gameService,
            _intentionManager,
            _repository);
    }

    /// <summary>
    /// A caller with no version never read the sheet they are replacing, and
    /// the replacement deletes values.
    /// </summary>
    [Fact]
    public async Task RefuseABodyWithoutAVersionBeforeReadingTheGame()
    {
        var replace = async () => await _service.ReplaceAsync(new ReplaceGameAttributeSchema
        {
            GameId = _gameId,
            Specifications = []
        });

        await replace.Should().ThrowAsync<ValidationException>();
        await _gameService.DidNotReceive().GetAsync(Arg.Any<Guid>());
        await _repository.DidNotReceive().Replace(Arg.Any<ReplaceGameAttributeSchema>());
    }

    /// <summary>
    /// One identifier twice asks for two specifications that are one, and the
    /// values written against it would answer to both.
    /// </summary>
    [Fact]
    public async Task RefuseARepeatedIdentifierBeforeReadingTheGame()
    {
        var repeated = Guid.NewGuid();

        var replace = async () => await _service.ReplaceAsync(new ReplaceGameAttributeSchema
        {
            GameId = _gameId,
            Version = 0,
            Specifications =
            [
                Specification("Раса", repeated, 0),
                Specification("Класс", repeated, 1)
            ]
        });

        await replace.Should().ThrowAsync<ValidationException>();
        await _gameService.DidNotReceive().GetAsync(Arg.Any<Guid>());
        await _repository.DidNotReceive().Replace(Arg.Any<ReplaceGameAttributeSchema>());
    }

    /// <summary>
    /// The sheet is part of the game, so it is written by whoever may write the
    /// game - and nothing is written for anybody else.
    /// </summary>
    [Fact]
    public async Task RefuseACallerWhoMayNotEditTheGame()
    {
        _intentionManager
            .When(m => m.ThrowIfForbidden(GameIntention.Edit, Arg.Any<GameDto>()))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нет прав"));

        var replace = async () => await _service.ReplaceAsync(Valid());

        (await replace.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Replace(Arg.Any<ReplaceGameAttributeSchema>());
    }

    /// <summary>
    /// The game is resolved by the same read the details use, so a game this
    /// caller cannot see refuses the replacement the way it refuses a read.
    /// </summary>
    [Fact]
    public async Task RefuseAGameTheCallerCannotSee()
    {
        _gameService.GetAsync(_gameId)
            .Returns<GameDto>(_ => throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound));

        var replace = async () => await _service.ReplaceAsync(Valid());

        (await replace.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _repository.DidNotReceive().Replace(Arg.Any<ReplaceGameAttributeSchema>());
    }

    /// <summary>
    /// Nothing was written, so the caller is told to look again rather than
    /// left believing their set is stored.
    /// </summary>
    [Fact]
    public async Task RefuseAVersionTheStoredSheetHasMovedPast()
    {
        _repository.Replace(Arg.Any<ReplaceGameAttributeSchema>())
            .Returns((GameAttributeSchema?)null);

        var replace = async () => await _service.ReplaceAsync(Valid());

        (await replace.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerWithTheStoredSheetAndItsNewVersion()
    {
        var replaced = await _service.ReplaceAsync(Valid());

        replaced.Version.Should().Be(1);
        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Edit, Arg.Any<GameDto>());
    }

    private ReplaceGameAttributeSchema Valid() => new()
    {
        GameId = _gameId,
        Version = 0,
        Specifications = [Specification("Раса", null, 0)]
    };

    private static UpdateAttributeSpecification Specification(string title, Guid? id, int order) => new()
    {
        Id = id,
        Title = title,
        Type = AttributeSpecificationType.Text,
        Order = order
    };
}
