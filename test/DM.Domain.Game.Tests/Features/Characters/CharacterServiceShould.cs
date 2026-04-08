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
using DM.Domain.Game.Tests.Dsl;
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
        attributeValueFiller.Setup(f => f.Fill(It.IsAny<IEnumerable<Character>>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        var intentionConverter = Mock<ICharacterIntentionConverter>();
        intentionConverter.Setup(c => c.Convert(It.IsAny<CharacterStatus>(), It.IsAny<CharacterStatus>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns((CharacterIntention.Edit, EventType.ChangedCharacter));

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.Setup(r => r.IncrementAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);

        var subscriptionService = Mock<IGameSubscriptionService>();

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _producer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(_currentUserId, UserRole.RegularUser));

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
            intentionConverter.Object,
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
    public async Task AuthorizeDeleteCharacterAction()
    {
        var characterId = Guid.NewGuid();
        var character = new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid() };
        _repository.Setup(r => r.GetForUpdate(characterId)).ReturnsAsync(character);
        _repository.Setup(r => r.Delete(characterId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(characterId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CharacterIntention.Delete, character), Times.Once);
    }
}
