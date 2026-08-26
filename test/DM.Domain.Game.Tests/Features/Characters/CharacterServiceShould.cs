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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CharacterServiceShould : UnitTestBase
{
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly ICharacterRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CharacterService _service;
    private readonly Guid _currentUserId;

    public CharacterServiceShould()
    {
        var createValidator = Mock<IValidator<CreateCharacter>>();
        createValidator.ValidateAsync(Arg.Any<ValidationContext<CreateCharacter>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateCharacter>>();
        updateValidator.ValidateAsync(Arg.Any<ValidationContext<UpdateCharacter>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<ICharacterRepository>();

        var attributeValueFiller = Mock<ICharacterAttributeValueFiller>();
        attributeValueFiller.Fill(Arg.Any<IEnumerable<Character>>(), Arg.Any<GameDto>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.IncrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>())
            .Returns(Task.CompletedTask);

        var subscriptionService = Mock<IGameSubscriptionService>();

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);
        _producer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new CharacterService(
            createValidator,
            updateValidator,
            _gameService,
            _intentionManager,
            _repository,
            attributeValueFiller,
            _unreadCountersRepository,
            subscriptionService,
            _producer,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider);
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
        _gameService.GetAsync(gameId).Returns(game);
        _repository.Create(Arg.Any<CreateCharacterEntity>()).Returns(new Character { Id = Guid.NewGuid() });

        await _service.CreateAsync(createCharacter);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.CreateCharacter, game);
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
        _gameService.GetAsync(gameId).Returns(game);
        _repository.Create(Arg.Any<CreateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.CreateAsync(createCharacter);

        await _producer.Received(1).SendAsync(EventType.NewCharacter, characterId);
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
        _gameService.GetAsync(gameId).Returns(game);

        var act = async () => await _service.CreateAsync(createCharacter);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowNotFoundWhenCharacterDoesNotExist()
    {
        var characterId = Guid.NewGuid();
        _repository.FindCharacter(characterId).Returns((Character?)null);

        var act = async () => await _service.GetAsync(characterId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// An identifier nobody answers to is a 404, on every verb.
    /// </summary>
    /// <remarks>
    /// The read behind these two used to materialize with FirstAsync, so an
    /// unknown character reached the caller as a server fault while the endpoint
    /// documented 404. Fixed where it started rather than guarded at each call
    /// site: the third caller had already grown a second query of its own to work
    /// around exactly this.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnswerNotFoundWhenTheCharacterIsUnknown(bool deleting)
    {
        var characterId = Guid.NewGuid();
        _repository.GetForUpdate(characterId).Returns((CharacterToUpdate?)null);

        Func<Task> act = deleting
            ? () => _service.DeleteAsync(characterId)
            : () => _service.UpdateAsync(new UpdateCharacter { CharacterId = characterId, Name = "Any" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeUpdateCharacterAction()
    {
        var characterId = Guid.NewGuid();
        var updateCharacter = new UpdateCharacter { CharacterId = characterId, Name = "Updated Character" };
        var characterForUpdate = new CharacterToUpdate { Id = characterId };
        _repository.GetForUpdate(characterId).Returns(characterForUpdate);
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.UpdateAsync(updateCharacter);

        _intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.Edit, characterForUpdate);
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
        _gameService.GetAsync(gameId).Returns(game);

        var characterForUpdate = new CharacterToUpdate
        {
            Id = characterId,
            GameId = gameId,
            GameMasterId = _currentUserId
        };
        _repository.GetForUpdate(characterId).Returns(characterForUpdate);

        UpdateCharacterEntity? captured = null;
        _repository.Update(Arg.Any<UpdateCharacterEntity>())
            .Returns(new Character { Id = characterId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateCharacterEntity>(0);
                captured = e;
            });

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
        await _repository.DidNotReceive().GetGameSchema(Arg.Any<Guid>());
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
        _gameService.GetAsync(gameId).Returns(game);

        var characterForUpdate = new CharacterToUpdate
        {
            Id = characterId,
            GameId = gameId,
            AuthorId = authorId,
            GameMasterId = game.Master.UserId
        };
        _repository.GetForUpdate(characterId).Returns(characterForUpdate);

        var schema = new AttributeSchema
        {
            Specifications = new[]
            {
                new AttributeSpecification { Id = visibleSpecId, Title = "V", IsHidden = false },
                new AttributeSpecification { Id = hiddenSpecId, Title = "H", IsHidden = true }
            }
        };
        _repository.GetGameSchema(gameId).Returns(schema);

        _repository.FindCharacter(characterId).Returns(new Character
        {
            Id = characterId,
            GameId = gameId,
            Attributes = new[]
            {
                new Domain.Game.Features.Games.CharacterAttribute { Id = hiddenSpecId, Value = "secret" }
            }
        });

        UpdateCharacterEntity? captured = null;
        _repository.Update(Arg.Any<UpdateCharacterEntity>())
            .Returns(new Character { Id = characterId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateCharacterEntity>(0);
                captured = e;
            });

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
        _repository.GetForUpdate(characterId).Returns(character);
        _repository.Delete(characterId, _currentUserId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(characterId);

        _intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.Delete, character);
        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await _repository.Received(1).Delete(characterId, _currentUserId);
    }

    /// <summary>
    /// A field the caller may not set is refused, not dropped. The update asked
    /// IsAllowed and, on a no, left the value out: the request came back 200 with
    /// the character unchanged, which is exactly what a successful edit looks like.
    /// The same shape guarded the privacy policy beside this flag, the closed and
    /// attached flags of a forum topic and the text of a game post; each of those
    /// is held by a test of its own, because one test for a shape leaves the other
    /// three points free to go back to dropping in silence.
    /// </summary>
    [Fact]
    public async Task RefuseAnNpcFlagTheCallerMayNotSet()
    {
        var characterId = Guid.NewGuid();
        _repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid(), IsNpc = false });
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            IsNpc = true
        });

        _intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.EditMasterSettings);
    }

    /// <summary>
    /// The client round-trips the whole character, so a value it already has is
    /// nobody's attempt at anything and must not be refused.
    /// </summary>
    [Fact]
    public async Task NotAskForMasterSettingsWhenTheNpcFlagIsUnchanged()
    {
        var characterId = Guid.NewGuid();
        _repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid(), IsNpc = true });
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            IsNpc = true
        });

        _intentionManager.DidNotReceive().ThrowIfForbidden(CharacterIntention.EditMasterSettings);
    }

    /// <summary>
    /// The second field on the same update, held separately: who may see the
    /// character is a privacy decision, and answering a denied change with the
    /// old policy and a 200 tells the owner it is set the way he asked.
    /// </summary>
    [Fact]
    public async Task RefuseAnAccessPolicyTheCallerMayNotSet()
    {
        var characterId = Guid.NewGuid();
        _repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate
            {
                Id = characterId,
                GameId = Guid.NewGuid(),
                AccessPolicy = CharacterAccessPolicy.NoAccess
            });
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            AccessPolicy = CharacterAccessPolicy.EditAllowed
        });

        _intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.EditPrivacySettings);
    }

    /// <summary>
    /// And the same exemption: the round-tripped policy is not a change.
    /// </summary>
    [Fact]
    public async Task NotAskForPrivacySettingsWhenTheAccessPolicyIsUnchanged()
    {
        var characterId = Guid.NewGuid();
        _repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate
            {
                Id = characterId,
                GameId = Guid.NewGuid(),
                AccessPolicy = CharacterAccessPolicy.EditAllowed
            });
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            AccessPolicy = CharacterAccessPolicy.EditAllowed
        });

        _intentionManager.DidNotReceive().ThrowIfForbidden(CharacterIntention.EditPrivacySettings);
    }
}
