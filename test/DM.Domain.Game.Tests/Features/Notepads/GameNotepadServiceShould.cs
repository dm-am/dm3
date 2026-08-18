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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Notepads;

public class GameNotepadServiceShould : UnitTestBase
{
    private readonly Mock<INotepadRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IGameService> _gameService;
    private readonly Mock<ICharacterService> _characterService;
    private readonly GameNotepadService _service;
    private readonly Guid _currentUserId;

    public GameNotepadServiceShould()
    {
        _repository = Mock<INotepadRepository>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<NotepadIntention>(), It.IsAny<NotepadAuthContext>()));

        _gameService = Mock<IGameService>();
        _characterService = Mock<ICharacterService>();

        _service = new GameNotepadService(
            _repository.Object,
            _identityProvider.Object,
            guidFactory.Object,
            dateTimeProvider.Object,
            _intentionManager.Object,
            _gameService.Object,
            _characterService.Object);
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
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.GetEntriesAsync(NotepadType.Master, gameId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotepadEntry>());

        await _service.GetMasterEntries(gameId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(NotepadIntention.Read, It.IsAny<NotepadAuthContext>()), Times.Once);
    }

    [Fact]
    public async Task CreateMasterEntry()
    {
        var gameId = Guid.NewGuid();
        var createEntry = new CreateNotepadEntry { Title = "Test Entry", Content = "Content" };
        var game = CreateGame(gameId);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.CreateEntryAsync(It.IsAny<CreateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotepadEntry { Id = Guid.NewGuid() });

        var result = await _service.CreateMasterEntry(gameId, createEntry);

        result.Should().NotBeNull();
        _repository.Verify(r => r.CreateEntryAsync(
            It.Is<CreateNotepadEntryInternal>(e => e.NotepadType == NotepadType.Master && e.ContainerId == gameId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeGetPlayerEntriesAction()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _characterService.Setup(s => s.GetAsync(characterId)).ReturnsAsync(character);
        _repository.Setup(r => r.GetEntriesAsync(NotepadType.Player, gameId, characterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotepadEntry>());

        await _service.GetPlayerEntries(gameId, characterId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(NotepadIntention.Read, It.IsAny<NotepadAuthContext>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlayerEntry()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createEntry = new CreateNotepadEntry { Title = "Test Entry", Content = "Content" };
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _characterService.Setup(s => s.GetAsync(characterId)).ReturnsAsync(character);
        _repository.Setup(r => r.CreateEntryAsync(It.IsAny<CreateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotepadEntry { Id = Guid.NewGuid() });

        var result = await _service.CreatePlayerEntry(gameId, characterId, createEntry);

        result.Should().NotBeNull();
        _repository.Verify(r => r.CreateEntryAsync(
            It.Is<CreateNotepadEntryInternal>(e =>
                e.NotepadType == NotepadType.Player &&
                e.ContainerId == gameId &&
                e.OwnerId == characterId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeGetCharacterMasterEntriesAction()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _characterService.Setup(s => s.GetAsync(characterId)).ReturnsAsync(character);
        _repository.Setup(r => r.GetEntriesAsync(NotepadType.CharacterMaster, gameId, characterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotepadEntry>());

        await _service.GetCharacterMasterEntries(gameId, characterId);

        // Scoped to the character and to this notepad type. Read against the
        // player scope instead, the notes the leads keep would answer with what
        // the player wrote — the two notepads share a table and a character.
        _repository.Verify(r => r.GetEntriesAsync(
            NotepadType.CharacterMaster, gameId, characterId, It.IsAny<CancellationToken>()), Times.Once);
        _intentionManager.Verify(m => m.ThrowIfForbidden(
            NotepadIntention.Read,
            It.Is<NotepadAuthContext>(c =>
                c.NotepadType == NotepadType.CharacterMaster && c.OwnerId == characterId)), Times.Once);
    }

    [Fact]
    public async Task CreateCharacterMasterEntry()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createEntry = new CreateNotepadEntry { Title = "Test Entry", Content = "Content" };
        var game = CreateGame(gameId);
        var character = new Character { Id = characterId, GameId = gameId };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _characterService.Setup(s => s.GetAsync(characterId)).ReturnsAsync(character);
        _repository.Setup(r => r.CreateEntryAsync(It.IsAny<CreateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotepadEntry { Id = Guid.NewGuid() });

        var result = await _service.CreateCharacterMasterEntry(gameId, characterId, createEntry);

        result.Should().NotBeNull();
        _repository.Verify(r => r.CreateEntryAsync(
            It.Is<CreateNotepadEntryInternal>(e =>
                e.NotepadType == NotepadType.CharacterMaster &&
                e.ContainerId == gameId &&
                e.OwnerId == characterId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecideTheNotesKeptAboutACharacterWithoutAskingWhoOwnsIt()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.GetEntriesAsync(NotepadType.CharacterMaster, gameId, characterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotepadEntry>());

        await _service.GetCharacterMasterEntries(gameId, characterId);

        // This notepad is answered by the game role alone, which is what makes
        // it exist for every character rather than for the ones with a player.
        // A character owner in the context would be a second way in.
        _intentionManager.Verify(m => m.ThrowIfForbidden(
            NotepadIntention.Read,
            It.Is<NotepadAuthContext>(c => c.CharacterOwnerId == null)), Times.Once);
        _characterService.Verify(s => s.GetAsync(It.IsAny<Guid>()), Times.Never);
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
        _repository.Setup(r => r.GetEntryAsync(entryId, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(CreateGame(gameId));

        // The entry endpoints take an id and nothing else, so the type guard in
        // front of them is what decides whether this service answers for the
        // entry at all. A notepad it serves must pass it.
        var result = await _service.GetEntry(entryId);

        result.Should().BeSameAs(entry);
        _intentionManager.Verify(m => m.ThrowIfForbidden(NotepadIntention.Read, It.IsAny<NotepadAuthContext>()), Times.Once);
    }

    [Fact]
    public async Task ThrowNotFoundWhenEntryDoesNotExist()
    {
        var entryId = Guid.NewGuid();
        _repository.Setup(r => r.GetEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotepadEntry?)null);

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
        _repository.Setup(r => r.GetEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

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
        _repository.Setup(r => r.GetEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.UpdateEntryAsync(It.IsAny<UpdateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        await _service.UpdateEntry(entryId, updateEntry);

        _intentionManager.Verify(m => m.ThrowIfForbidden(NotepadIntention.Edit, It.IsAny<NotepadAuthContext>()), Times.Once);
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
        _repository.Setup(r => r.GetEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.DeleteEntryAsync(entryId, _currentUserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.DeleteEntry(entryId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(NotepadIntention.Delete, It.IsAny<NotepadAuthContext>()), Times.Once);
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
        _repository.Setup(r => r.GetEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.UpdateEntryAsync(It.IsAny<UpdateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        await _service.UpdateEntry(entryId, updateEntry);

        // Editing belongs to the author alone. Left out of the context, the
        // resolver has nobody to compare the asker against and the rule cannot
        // be stated at all.
        _intentionManager.Verify(m => m.ThrowIfForbidden(
            NotepadIntention.Edit,
            It.Is<NotepadAuthContext>(c => c.AuthorId == authorId)), Times.Once);
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
        _repository.Setup(r => r.GetEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.DeleteEntryAsync(entryId, _currentUserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.DeleteEntry(entryId);

        // Deleting is the author's and the game master's, and the first half of
        // that needs the author in the context just as much as editing does.
        _intentionManager.Verify(m => m.ThrowIfForbidden(
            NotepadIntention.Delete,
            It.Is<NotepadAuthContext>(c => c.AuthorId == authorId)), Times.Once);
    }

    [Fact]
    public async Task LeaveTheAuthorUnsetWhenAuthorizingTheNotepadItself()
    {
        var gameId = Guid.NewGuid();
        var game = CreateGame(gameId);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.CreateEntryAsync(It.IsAny<CreateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotepadEntry { Id = Guid.NewGuid() });

        await _service.CreateMasterEntry(gameId, new CreateNotepadEntry { Title = "Test Entry", Content = "Content" });

        // Creating is not about an entry that exists, so there is no author yet
        // and the resolver must not be handed one.
        _intentionManager.Verify(m => m.ThrowIfForbidden(
            NotepadIntention.Create,
            It.Is<NotepadAuthContext>(c => c.AuthorId == null)), Times.Once);
    }
}
