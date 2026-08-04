using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CharacterServiceShould : UnitTestBase
{
    private readonly Mock<IGameService> _gameService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<ICharacterRepository> _repository;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly CharacterService _service;
    private readonly Guid _currentUserId;

    public CharacterServiceShould()
    {
        var createValidator = Mock<IValidator<CreateCharacter>>();
        createValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateCharacter>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateCharacter>>();
        updateValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateCharacter>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<CharacterIntention>(), It.IsAny<CharacterToUpdate>()));

        _repository = Mock<ICharacterRepository>();

        var attributeValueFiller = Mock<ICharacterAttributeValueFiller>();
        attributeValueFiller.Setup(f => f.Fill(It.IsAny<IEnumerable<Character>>(), It.IsAny<GameDto>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.Setup(r => r.IncrementAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);

        var subscriptionService = Mock<IGameSubscriptionService>();

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _producer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new CharacterService(
            createValidator.Object,
            updateValidator.Object,
            _gameService.Object,
            _intentionManager.Object,
            _repository.Object,
            attributeValueFiller.Object,
            _unreadCountersRepository.Object,
            subscriptionService.Object,
            _producer.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task AuthorizeCreateCharacterAction()
    {
        var gameId = Guid.NewGuid();
        var createCharacter = new CreateCharacter { GameId = gameId, Name = "Test Character" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.Create(It.IsAny<CreateCharacterEntity>()))
            .ReturnsAsync(new Character { Id = Guid.NewGuid() });

        await _service.CreateAsync(createCharacter);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.CreateCharacter, game), Times.Once);
    }

    [Fact]
    public async Task CreateCharacterAndPublishEvent()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createCharacter = new CreateCharacter { GameId = gameId, Name = "Test Character" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.Create(It.IsAny<CreateCharacterEntity>()))
            .ReturnsAsync(new Character { Id = characterId });

        await _service.CreateAsync(createCharacter);

        _producer.Verify(p => p.SendAsync(EventType.NewCharacter, characterId), Times.Once);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUserIsBlacklisted()
    {
        var gameId = Guid.NewGuid();
        var createCharacter = new CreateCharacter { GameId = gameId, Name = "Test Character" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = new[] { new BlacklistedUser { UserId = _currentUserId } }
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        var act = async () => await _service.CreateAsync(createCharacter);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowNotFoundWhenCharacterDoesNotExist()
    {
        var characterId = Guid.NewGuid();
        _repository.Setup(r => r.FindCharacter(characterId)).ReturnsAsync((Character?)null);

        var act = async () => await _service.GetAsync(characterId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeUpdateCharacterAction()
    {
        var characterId = Guid.NewGuid();
        var updateCharacter = new UpdateCharacter { CharacterId = characterId, Name = "Updated Character" };
        var characterForUpdate = new CharacterToUpdate { Id = characterId };
        _repository.Setup(r => r.GetForUpdate(characterId)).ReturnsAsync(characterForUpdate);
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .ReturnsAsync(new Character { Id = characterId });

        await _service.UpdateAsync(updateCharacter);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CharacterIntention.Edit, characterForUpdate), Times.Once);
    }

    [Fact]
    public async Task PreserveHiddenAttributeWhenMasterOmitsItOnUpdate()
    {
        // A game lead can see hidden values, so the submission passes through
        // unchanged. Omitting the hidden attribute must not blank it: the entity
        // sent to the repository carries only the submitted (visible) values,
        // leaving the stored hidden value untouched.
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var visibleSpecId = Guid.NewGuid();
        var hiddenSpecId = Guid.NewGuid();

        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = _currentUserId, Username = "Master" },
            Assistants = [],
            Players = [],
            AttributeSchemaId = Guid.NewGuid()
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        var characterForUpdate = new CharacterToUpdate
        {
            Id = characterId,
            GameId = gameId,
            GameMasterId = _currentUserId
        };
        _repository.Setup(r => r.GetForUpdate(characterId)).ReturnsAsync(characterForUpdate);

        UpdateCharacterEntity? captured = null;
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .Callback<UpdateCharacterEntity>(e => captured = e)
            .ReturnsAsync(new Character { Id = characterId });

        var updateCharacter = new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            Attributes = new[]
            {
                new Domain.Game.Features.Games.CharacterAttribute { Id = visibleSpecId, Value = "visible" }
            }
        };

        await _service.UpdateAsync(updateCharacter);

        captured.Should().NotBeNull();
        captured!.Attributes.Should().ContainSingle(a => a.Id == visibleSpecId && a.Value == "visible");
        captured.Attributes.Should().NotContain(a => a.Id == hiddenSpecId);
        // Lead can see hidden values -> no schema/stored lookup needed.
        _repository.Verify(r => r.GetGameSchema(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RestoreHiddenAttributeWhenSubmitterCannotSeeItOnUpdate()
    {
        // A submitter whose read was redacted (neither owner nor lead) must not
        // be able to blank or overwrite a hidden value. Their hidden input is
        // dropped and the stored value is restored so it survives the round-trip.
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var visibleSpecId = Guid.NewGuid();
        var hiddenSpecId = Guid.NewGuid();

        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Master" },
            Assistants = [],
            Players = [],
            AttributeSchemaId = Guid.NewGuid()
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        var characterForUpdate = new CharacterToUpdate
        {
            Id = characterId,
            GameId = gameId,
            AuthorId = authorId,
            GameMasterId = game.Master.UserId
        };
        _repository.Setup(r => r.GetForUpdate(characterId)).ReturnsAsync(characterForUpdate);

        var schema = new AttributeSchema
        {
            Specifications = new[]
            {
                new AttributeSpecification { Id = visibleSpecId, Title = "V", IsHidden = false },
                new AttributeSpecification { Id = hiddenSpecId, Title = "H", IsHidden = true }
            }
        };
        _repository.Setup(r => r.GetGameSchema(gameId)).ReturnsAsync(schema);

        _repository.Setup(r => r.FindCharacter(characterId)).ReturnsAsync(new Character
        {
            Id = characterId,
            GameId = gameId,
            Attributes = new[]
            {
                new Domain.Game.Features.Games.CharacterAttribute { Id = hiddenSpecId, Value = "secret" }
            }
        });

        UpdateCharacterEntity? captured = null;
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .Callback<UpdateCharacterEntity>(e => captured = e)
            .ReturnsAsync(new Character { Id = characterId });

        var updateCharacter = new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            Attributes = new[]
            {
                new Domain.Game.Features.Games.CharacterAttribute { Id = visibleSpecId, Value = "visible" },
                // Submitter tries to blank the hidden value they could not see.
                new Domain.Game.Features.Games.CharacterAttribute { Id = hiddenSpecId, Value = string.Empty }
            }
        };

        await _service.UpdateAsync(updateCharacter);

        captured.Should().NotBeNull();
        captured!.Attributes.Should().Contain(a => a.Id == visibleSpecId && a.Value == "visible");
        captured.Attributes.Should().Contain(a => a.Id == hiddenSpecId && a.Value == "secret");
    }

    [Fact]
    public async Task AuthorizeDeleteCharacterAction()
    {
        var characterId = Guid.NewGuid();
        var character = new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid() };
        _repository.Setup(r => r.GetForUpdate(characterId)).ReturnsAsync(character);
        _repository.Setup(r => r.Delete(characterId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(characterId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CharacterIntention.Delete, character), Times.Once);
    }

    /// <summary>
    /// A field the caller may not set is refused, not dropped. The update asked
    /// IsAllowed and, on a no, left the value out: the request came back 200 with
    /// the character unchanged, which is exactly what a successful edit looks like.
    /// One test for the pattern - the same shape guarded the privacy policy beside
    /// this flag, the closed and attached flags of a forum topic, and the text of a
    /// game post.
    /// </summary>
    [Fact]
    public async Task RefuseAnNpcFlagTheCallerMayNotSet()
    {
        var characterId = Guid.NewGuid();
        _repository.Setup(r => r.GetForUpdate(characterId)).ReturnsAsync(
            new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid(), IsNpc = false });
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .ReturnsAsync(new Character { Id = characterId });

        await _service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            IsNpc = true
        });

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(CharacterIntention.EditMasterSettings), Times.Once);
    }

    /// <summary>
    /// The client round-trips the whole character, so a value it already has is
    /// nobody's attempt at anything and must not be refused.
    /// </summary>
    [Fact]
    public async Task NotAskForMasterSettingsWhenTheNpcFlagIsUnchanged()
    {
        var characterId = Guid.NewGuid();
        _repository.Setup(r => r.GetForUpdate(characterId)).ReturnsAsync(
            new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid(), IsNpc = true });
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .ReturnsAsync(new Character { Id = characterId });

        await _service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            IsNpc = true
        });

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(CharacterIntention.EditMasterSettings), Times.Never);
    }
}
