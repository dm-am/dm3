using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Notepads;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Notepads;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Notepads;

public class GameNotepadServiceShould : UnitTestBase
{
    private readonly INotepadRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameService _gameService;
    private readonly ICharacterService _characterService;
    private readonly GameNotepadService _service;
    private readonly Guid _currentUserId;

    public GameNotepadServiceShould()
    {
        _repository = Mock<INotepadRepository>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _intentionManager = Mock<IIntentionManager>();

        _gameService = Mock<IGameService>();
        _characterService = Mock<ICharacterService>();

        _service = new GameNotepadService(
            _repository,
            _identityProvider,
            guidFactory,
            dateTimeProvider,
            _intentionManager,
            _gameService,
            _characterService);
    }

    private static GameDetails CreateGame(Guid gameId) => new GameDetails
    {
        Id = gameId,
        Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
    };

    [Fact]
    public async Task AuthorizeGetMasterEntriesAction()
    {
        var gameId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.GetEntriesAsync(NotepadType.Master, gameId, null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NotepadEntry>());

        await _service.GetMasterEntries(gameId);

        _intentionManager.Received(1).ThrowIfForbidden(NotepadIntention.Read, Arg.Any<NotepadAuthContext>());
    }

    [Fact]
    public async Task CreateMasterEntry()
    {
        var gameId = Guid.NewGuid();
        var createEntry = new CreateNotepadEntry { Title = "Test Entry", Content = "Content" };
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.CreateEntryAsync(Arg.Any<CreateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(new NotepadEntry { Id = Guid.NewGuid() });

        var result = await _service.CreateMasterEntry(gameId, createEntry);

        result.Should().NotBeNull();
        await _repository.Received(1).CreateEntryAsync(
            Arg.Is<CreateNotepadEntryInternal>(e => e.NotepadType == NotepadType.Master && e.ContainerId == gameId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeGetPlayerEntriesAction()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.GetAsync(gameId).Returns(game);
        _characterService.GetAsync(characterId).Returns(character);
        _repository.GetEntriesAsync(NotepadType.Player, gameId, characterId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NotepadEntry>());

        await _service.GetPlayerEntries(gameId, characterId);

        _intentionManager.Received(1).ThrowIfForbidden(NotepadIntention.Read, Arg.Any<NotepadAuthContext>());
    }

    [Fact]
    public async Task CreatePlayerEntry()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createEntry = new CreateNotepadEntry { Title = "Test Entry", Content = "Content" };
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.GetAsync(gameId).Returns(game);
        _characterService.GetAsync(characterId).Returns(character);
        _repository.CreateEntryAsync(Arg.Any<CreateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(new NotepadEntry { Id = Guid.NewGuid() });

        var result = await _service.CreatePlayerEntry(gameId, characterId, createEntry);

        result.Should().NotBeNull();
        await _repository.Received(1).CreateEntryAsync(
            Arg.Is<CreateNotepadEntryInternal>(e =>
                e.NotepadType == NotepadType.Player &&
                e.ContainerId == gameId &&
                e.OwnerId == characterId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeGetCharacterMasterEntriesAction()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.GetAsync(gameId).Returns(game);
        _characterService.GetAsync(characterId).Returns(character);
        _repository.GetEntriesAsync(NotepadType.CharacterMaster, gameId, characterId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NotepadEntry>());

        await _service.GetCharacterMasterEntries(gameId, characterId);

        // Scoped to the character and to this notepad type. Read against the
        // player scope instead, the notes the leads keep would answer with what
        // the player wrote — the two notepads share a table and a character.
        await _repository.Received(1).GetEntriesAsync(
            NotepadType.CharacterMaster, gameId, characterId, Arg.Any<CancellationToken>());
        _intentionManager.Received(1).ThrowIfForbidden(
            NotepadIntention.Read,
            Arg.Is<NotepadAuthContext>(c =>
                c.NotepadType == NotepadType.CharacterMaster && c.OwnerId == characterId));
    }

    [Fact]
    public async Task CreateCharacterMasterEntry()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createEntry = new CreateNotepadEntry { Title = "Test Entry", Content = "Content" };
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.GetAsync(gameId).Returns(game);
        _characterService.GetAsync(characterId).Returns(character);
        _repository.CreateEntryAsync(Arg.Any<CreateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(new NotepadEntry { Id = Guid.NewGuid() });

        var result = await _service.CreateCharacterMasterEntry(gameId, characterId, createEntry);

        result.Should().NotBeNull();
        await _repository.Received(1).CreateEntryAsync(
            Arg.Is<CreateNotepadEntryInternal>(e =>
                e.NotepadType == NotepadType.CharacterMaster &&
                e.ContainerId == gameId &&
                e.OwnerId == characterId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideTheNotesKeptAboutACharacterWithoutAskingWhoOwnsIt()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.GetEntriesAsync(NotepadType.CharacterMaster, gameId, characterId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<NotepadEntry>());

        await _service.GetCharacterMasterEntries(gameId, characterId);

        // This notepad is answered by the game role alone, which is what makes
        // it exist for every character rather than for the ones with a player.
        // A character owner in the context would be a second way in.
        _intentionManager.Received(1).ThrowIfForbidden(
            NotepadIntention.Read,
            Arg.Is<NotepadAuthContext>(c => c.CharacterOwnerId == null));
        await _characterService.DidNotReceive().GetAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task AuthorizeAnEntryOfTheNotesKeptAboutACharacter()
    {
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.CharacterMaster,
            ContainerId = gameId,
            OwnerId = characterId,
            AuthorId = Guid.NewGuid()
        };
        _repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);
        _gameService.GetAsync(gameId).Returns(CreateGame(gameId));

        // The entry endpoints take an id and nothing else, so the type guard in
        // front of them is what decides whether this service answers for the
        // entry at all. A notepad it serves must pass it.
        var result = await _service.GetEntry(entryId);

        result.Should().BeSameAs(entry);
        _intentionManager.Received(1).ThrowIfForbidden(NotepadIntention.Read, Arg.Any<NotepadAuthContext>());
    }

    [Fact]
    public async Task ThrowNotFoundWhenEntryDoesNotExist()
    {
        var entryId = Guid.NewGuid();
        _repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns((NotepadEntry?)null);

        var act = async () => await _service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowForbiddenWhenAccessingNonGameNotepadType()
    {
        var entryId = Guid.NewGuid();
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.User,
            ContainerId = Guid.NewGuid()
        };
        _repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);

        var act = async () => await _service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthorizeUpdateEntryAction()
    {
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var updateEntry = new UpdateNotepadEntry { Title = "Updated Entry" };
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.Master,
            ContainerId = gameId
        };
        var game = CreateGame(gameId);
        _repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.UpdateEntryAsync(Arg.Any<UpdateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(entry);

        await _service.UpdateEntry(entryId, updateEntry);

        _intentionManager.Received(1).ThrowIfForbidden(NotepadIntention.Edit, Arg.Any<NotepadAuthContext>());
    }

    [Fact]
    public async Task AuthorizeDeleteEntryAction()
    {
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.Master,
            ContainerId = gameId
        };
        var game = CreateGame(gameId);
        _repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.DeleteEntryAsync(entryId, _currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.DeleteEntry(entryId);

        _intentionManager.Received(1).ThrowIfForbidden(NotepadIntention.Delete, Arg.Any<NotepadAuthContext>());
    }

    [Fact]
    public async Task NameTheEntryAuthorWhenAuthorizingAnUpdate()
    {
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var updateEntry = new UpdateNotepadEntry { Title = "Updated Entry" };
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.Master,
            ContainerId = gameId,
            AuthorId = authorId
        };
        var game = CreateGame(gameId);
        _repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.UpdateEntryAsync(Arg.Any<UpdateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(entry);

        await _service.UpdateEntry(entryId, updateEntry);

        // Editing belongs to the author alone. Left out of the context, the
        // resolver has nobody to compare the asker against and the rule cannot
        // be stated at all.
        _intentionManager.Received(1).ThrowIfForbidden(
            NotepadIntention.Edit,
            Arg.Is<NotepadAuthContext>(c => c.AuthorId == authorId));
    }

    [Fact]
    public async Task NameTheEntryAuthorWhenAuthorizingADeletion()
    {
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.Master,
            ContainerId = gameId,
            AuthorId = authorId
        };
        var game = CreateGame(gameId);
        _repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.DeleteEntryAsync(entryId, _currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.DeleteEntry(entryId);

        // Deleting is the author's and the game master's, and the first half of
        // that needs the author in the context just as much as editing does.
        _intentionManager.Received(1).ThrowIfForbidden(
            NotepadIntention.Delete,
            Arg.Is<NotepadAuthContext>(c => c.AuthorId == authorId));
    }

    [Fact]
    public async Task LeaveTheAuthorUnsetWhenAuthorizingTheNotepadItself()
    {
        var gameId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.GetAsync(gameId).Returns(game);
        _repository.CreateEntryAsync(Arg.Any<CreateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(new NotepadEntry { Id = Guid.NewGuid() });

        await _service.CreateMasterEntry(gameId, new CreateNotepadEntry { Title = "Test Entry", Content = "Content" });

        // Creating is not about an entry that exists, so there is no author yet
        // and the resolver must not be handed one.
        _intentionManager.Received(1).ThrowIfForbidden(
            NotepadIntention.Create,
            Arg.Is<NotepadAuthContext>(c => c.AuthorId == null));
    }
}
