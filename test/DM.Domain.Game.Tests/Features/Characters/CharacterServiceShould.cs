using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Blacklists;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CharacterServiceShould : CharacterServiceTestBase
{
    [Fact]
    public async Task StoreEveryValueAsTyped()
    {
        var gameId = Guid.NewGuid();
        var markupSpecId = Guid.NewGuid();
        var textSpecId = Guid.NewGuid();
        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));

        CreateCharacterEntity? captured = null;
        repository.Create(Arg.Do<CreateCharacterEntity>(e => captured = e))
            .Returns(new Character { Id = Guid.NewGuid(), GameId = gameId });

        await service.CreateAsync(new CreateCharacter
        {
            GameId = gameId,
            Name = "Герой",
            Attributes = new[]
            {
                new CharacterAttribute { Id = markupSpecId, Value = "[B]сила[/B]" },
                new CharacterAttribute { Id = textSpecId, Value = "[B]сила[/B]" }
            }
        });

        captured!.Attributes.Should().OnlyContain(a => a.Value == "[B]сила[/B]");
    }

    [Fact]
    public async Task AuthorizeCreateCharacterAction()
    {
        var gameId = Guid.NewGuid();
        var createCharacter = new CreateCharacter { GameId = gameId, Name = "Test Character" };
        var game = GameBuilder.Bare(gameId);
        gameService.GetAsync(gameId).Returns(game);
        repository.Create(Arg.Any<CreateCharacterEntity>()).Returns(new Character { Id = Guid.NewGuid() });

        await service.CreateAsync(createCharacter);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.CreateCharacter, game);
    }

    [Fact]
    public async Task CreateCharacterAndPublishEvent()
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createCharacter = new CreateCharacter { GameId = gameId, Name = "Test Character" };
        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        repository.Create(Arg.Any<CreateCharacterEntity>()).Returns(new Character { Id = characterId });

        await service.CreateAsync(createCharacter);

        await producer.Received(1).SendAsync(EventType.NewCharacter, characterId);
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
            BlacklistedUsers = new[] { new BlacklistedUser { UserId = currentUserId } }
        };
        gameService.GetAsync(gameId).Returns(game);
        // The gate carries the same rule and would refuse in generic words; the
        // list is asked first so the refusal says why.
        intentionManager
            .When(manager => manager.ThrowIfForbidden(GameIntention.CreateCharacter, Arg.Any<GameDto>()))
            .Do(_ => throw new IntentionManagerException(
                Identities.User(currentUserId, UserRole.RegularUser).User, GameIntention.CreateCharacter));

        var act = async () => await service.CreateAsync(createCharacter);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromGame);
        await repository.DidNotReceive().Create(Arg.Any<CreateCharacterEntity>());
    }

    // Editing one's own application is applying again, which the list closes.
    [Theory]
    [InlineData(CharacterStatus.UnderReview)]
    [InlineData(CharacterStatus.Declined)]
    public async Task RefuseTheBlacklistedOwnerAnEditOfHisApplication(CharacterStatus status)
    {
        var characterId = OwnCharacterInAGameThatListsTheOwner(status);

        var act = async () => await service.UpdateAsync(new UpdateCharacter { CharacterId = characterId, Name = "Other" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromGame);
        await repository.DidNotReceive().Update(Arg.Any<UpdateCharacterEntity>());
    }

    // The sheet of a character in play or out of it is not an application, and the list closes the
    // application only.
    [Theory]
    [InlineData(CharacterStatus.Active)]
    [InlineData(CharacterStatus.Retired)]
    public async Task LetTheBlacklistedOwnerEditASheetThatIsNoApplication(CharacterStatus status)
    {
        var characterId = OwnCharacterInAGameThatListsTheOwner(status);

        await service.UpdateAsync(new UpdateCharacter { CharacterId = characterId, Name = "Other" });

        await repository.Received(1).Update(Arg.Any<UpdateCharacterEntity>());
    }

    private Guid OwnCharacterInAGameThatListsTheOwner(CharacterStatus status)
    {
        var gameId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        repository.GetForUpdate(characterId).Returns(new CharacterToUpdate
        {
            Id = characterId,
            GameId = gameId,
            AuthorId = currentUserId,
            Status = status,
            GameAssistantIds = Array.Empty<Guid>()
        });
        gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            BlacklistedUsers = [new BlacklistedUser { UserId = currentUserId }]
        });
        return characterId;
    }

    [Fact]
    public async Task ThrowNotFoundWhenCharacterDoesNotExist()
    {
        var characterId = Guid.NewGuid();
        repository.FindCharacter(characterId).Returns((Character?)null);

        var act = async () => await service.GetAsync(characterId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnswerNotFoundWhenTheCharacterIsUnknown(bool deleting)
    {
        var characterId = Guid.NewGuid();
        repository.GetForUpdate(characterId).Returns((CharacterToUpdate?)null);

        Func<Task> act = deleting
            ? () => service.DeleteAsync(characterId)
            : () => service.UpdateAsync(new UpdateCharacter { CharacterId = characterId, Name = "Any" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeUpdateCharacterAction()
    {
        var characterId = Guid.NewGuid();
        var updateCharacter = new UpdateCharacter { CharacterId = characterId, Name = "Updated Character" };
        var characterForUpdate = new CharacterToUpdate { Id = characterId };
        repository.GetForUpdate(characterId).Returns(characterForUpdate);
        repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await service.UpdateAsync(updateCharacter);

        intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.Edit, characterForUpdate);
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
            Master = new GeneralUser { UserId = currentUserId, Username = "Master" },
            Assistants = [],
            Players = []
        };
        gameService.GetAsync(gameId).Returns(game);

        var characterForUpdate = new CharacterToUpdate
        {
            Id = characterId,
            GameId = gameId,
            GameMasterId = currentUserId
        };
        repository.GetForUpdate(characterId).Returns(characterForUpdate);

        UpdateCharacterEntity? captured = null;
        repository.Update(Arg.Do<UpdateCharacterEntity>(e => captured = e))
            .Returns(new Character { Id = characterId });

        var updateCharacter = new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            Attributes = new[]
            {
                new CharacterAttribute { Id = visibleSpecId, Value = "visible" }
            }
        };

        await service.UpdateAsync(updateCharacter);

        captured.Should().NotBeNull();
        captured!.Attributes.Should().ContainSingle(a => a.Id == visibleSpecId && a.Value == "visible");
        captured.Attributes.Should().NotContain(a => a.Id == hiddenSpecId);
        // Lead can see hidden values -> no schema/stored lookup needed.
        await repository.DidNotReceive().GetGameSchema(Arg.Any<Guid>());
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
            Players = []
        };
        gameService.GetAsync(gameId).Returns(game);

        var characterForUpdate = new CharacterToUpdate
        {
            Id = characterId,
            GameId = gameId,
            AuthorId = authorId,
            GameMasterId = game.Master.UserId
        };
        repository.GetForUpdate(characterId).Returns(characterForUpdate);

        var schema = new GameAttributeSchema
        {
            Specifications = new[]
            {
                new AttributeSpecification { Id = visibleSpecId, Title = "V", IsHidden = false },
                new AttributeSpecification { Id = hiddenSpecId, Title = "H", IsHidden = true }
            }
        };
        repository.GetGameSchema(gameId).Returns(schema);

        repository.FindCharacter(characterId).Returns(new Character
        {
            Id = characterId,
            GameId = gameId,
            Attributes = new[]
            {
                new CharacterAttribute { Id = hiddenSpecId, Value = "secret" }
            }
        });

        UpdateCharacterEntity? captured = null;
        repository.Update(Arg.Do<UpdateCharacterEntity>(e => captured = e))
            .Returns(new Character { Id = characterId });

        var updateCharacter = new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            Attributes = new[]
            {
                new CharacterAttribute { Id = visibleSpecId, Value = "visible" },
                // Submitter tries to blank the hidden value they could not see.
                new CharacterAttribute { Id = hiddenSpecId, Value = string.Empty }
            }
        };

        await service.UpdateAsync(updateCharacter);

        captured.Should().NotBeNull();
        captured!.Attributes.Should().Contain(a => a.Id == visibleSpecId && a.Value == "visible");
        captured.Attributes.Should().Contain(a => a.Id == hiddenSpecId && a.Value == "secret");
    }

    [Fact]
    public async Task AuthorizeDeleteCharacterAction()
    {
        var characterId = Guid.NewGuid();
        var character = new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid() };
        repository.GetForUpdate(characterId).Returns(character);
        repository.Delete(characterId, currentUserId).Returns(Task.CompletedTask);

        await service.DeleteAsync(characterId);

        intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.Delete, character);
        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await repository.Received(1).Delete(characterId, currentUserId);
    }

    // A field the caller may not set is refused, not dropped: a 200 with the character unchanged is
    // exactly what a successful edit looks like.
    [Fact]
    public async Task RefuseAnNpcFlagTheCallerMayNotSet()
    {
        var characterId = Guid.NewGuid();
        repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid(), IsNpc = false });
        repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            IsNpc = true
        });

        intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.EditMasterSettings);
    }

    // The client round-trips the whole character, so a value it already has is nobody's attempt at
    // anything and must not be refused.
    [Fact]
    public async Task NotAskForMasterSettingsWhenTheNpcFlagIsUnchanged()
    {
        var characterId = Guid.NewGuid();
        repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate { Id = characterId, GameId = Guid.NewGuid(), IsNpc = true });
        repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            IsNpc = true
        });

        intentionManager.DidNotReceive().ThrowIfForbidden(CharacterIntention.EditMasterSettings);
    }

    // The second field on the same update, held separately: who may see the character is a privacy
    // decision, and answering a denied change with the old policy and a 200 tells the owner it is
    // set the way he asked.
    [Fact]
    public async Task RefuseAnAccessPolicyTheCallerMayNotSet()
    {
        var characterId = Guid.NewGuid();
        repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate
            {
                Id = characterId,
                GameId = Guid.NewGuid(),
                AccessPolicy = CharacterAccessPolicy.NoAccess
            });
        repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            AccessPolicy = CharacterAccessPolicy.EditAllowed
        });

        intentionManager.Received(1).ThrowIfForbidden(CharacterIntention.EditPrivacySettings);
    }

    /// <summary>
    /// And the same exemption: the round-tripped policy is not a change.
    /// </summary>
    [Fact]
    public async Task NotAskForPrivacySettingsWhenTheAccessPolicyIsUnchanged()
    {
        var characterId = Guid.NewGuid();
        repository.GetForUpdate(characterId).Returns(
            new CharacterToUpdate
            {
                Id = characterId,
                GameId = Guid.NewGuid(),
                AccessPolicy = CharacterAccessPolicy.EditAllowed
            });
        repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await service.UpdateAsync(new UpdateCharacter
        {
            CharacterId = characterId,
            Name = "Updated",
            AccessPolicy = CharacterAccessPolicy.EditAllowed
        });

        intentionManager.DidNotReceive().ThrowIfForbidden(CharacterIntention.EditPrivacySettings);
    }
}
