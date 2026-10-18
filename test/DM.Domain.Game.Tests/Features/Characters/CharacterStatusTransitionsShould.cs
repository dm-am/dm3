using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Testing.Markup;
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Features.Characters;

/// <summary>
/// Every way out of a game, and every way back.
/// </summary>
/// <remarks>
/// Retirement had no working HTTP path at all. The update endpoint took a target
/// status with three booleans beside it and guessed the intent from the
/// combination; the mapping dropped all three booleans on the way in, so the
/// combination that arrived matched no arm and the converter threw a plain
/// exception — which is a 500. Nothing covered it: the converter was mocked in
/// the one place it appeared in tests, so the table it encoded was never checked
/// against anything.
///
/// The transition is now named by the caller and the table lives here. Each row
/// asserts the right the transition needs, since that is what differs between
/// three ways of reaching the same status: the lead kills and exiles, the player
/// leaves. The way back undoes only a move of the lead: Accept after Decline,
/// Resurrect after Kill, Readmit after Exile. What the player took out of the
/// game stays out.
/// </remarks>
public class CharacterStatusTransitionsShould : UnitTestBase
{
    private readonly ICharacterRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IEventProducer _producer;
    private readonly IGameSubscriptionService _subscriptions;
    private readonly IGameService _gameService;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ICharacterService _service;

    public CharacterStatusTransitionsShould()
    {
        var createValidator = Mock<IValidator<CreateCharacter>>();
        var updateValidator = Mock<IValidator<UpdateCharacter>>();
        _repository = Mock<ICharacterRepository>();
        _intentionManager = Mock<IIntentionManager>();
        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(_currentUserId));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _subscriptions = Mock<IGameSubscriptionService>();

        // A game that blacklisted nobody, unless a case says otherwise.
        _gameService = Mock<IGameService>();
        _gameService.GetAsync(Arg.Any<Guid>()).Returns(call => new GameDto { Id = call.Arg<Guid>() });

        _service = new CharacterService(
            createValidator,
            updateValidator,
            _gameService,
            _intentionManager,
            _repository,
            Mock<ICharacterAttributeValueFiller>(),
            Mock<IUnreadCountersRepository>(),
            _subscriptions,
            _producer,
            identityProvider,
            Mock<IGuidFactory>(),
            dateTimeProvider,
            new TagNamesAsTyped());
    }

    public static TheoryData<CharacterStatusTransition, CharacterStatus, CharacterIntention,
        CharacterStatus, EventType> LegalTransitions() => new()
    {
        { CharacterStatusTransition.Accept, CharacterStatus.UnderReview, CharacterIntention.Accept,
            CharacterStatus.Active, EventType.StatusCharacterAccepted },
        { CharacterStatusTransition.Accept, CharacterStatus.Declined, CharacterIntention.Accept,
            CharacterStatus.Active, EventType.StatusCharacterAccepted },
        { CharacterStatusTransition.Decline, CharacterStatus.UnderReview, CharacterIntention.Decline,
            CharacterStatus.Declined, EventType.StatusCharacterDeclined },
        { CharacterStatusTransition.Kill, CharacterStatus.Active, CharacterIntention.Kill,
            CharacterStatus.Retired, EventType.StatusCharacterDied },
        { CharacterStatusTransition.Exile, CharacterStatus.Active, CharacterIntention.Exile,
            CharacterStatus.Retired, EventType.StatusCharacterExiled },
        { CharacterStatusTransition.Leave, CharacterStatus.Active, CharacterIntention.Leave,
            CharacterStatus.Retired, EventType.StatusCharacterLeft },
        { CharacterStatusTransition.Resurrect, CharacterStatus.Retired, CharacterIntention.Resurrect,
            CharacterStatus.Active, EventType.StatusCharacterResurrected },
        { CharacterStatusTransition.Readmit, CharacterStatus.Retired, CharacterIntention.Readmit,
            CharacterStatus.Active, EventType.StatusCharacterReadmitted },
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public async Task AskTheRightPermissionAndWriteTheRightStatus(
        CharacterStatusTransition transition, CharacterStatus from, CharacterIntention intention,
        CharacterStatus to, EventType raised)
    {
        var characterId = Given(from);
        UpdateCharacterEntity? written = null;
        _repository.Update(Arg.Any<UpdateCharacterEntity>())
            .Returns(new Character { Id = characterId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateCharacterEntity>(0);
                written = e;
            });

        await _service.ChangeStatusAsync(characterId, transition);

        _intentionManager.Received(1).ThrowIfForbidden(intention, Arg.Any<CharacterToUpdate>());
        written!.Status.Should().Be(to);
        await _producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(raised)), characterId);
    }

    /// <summary>
    /// Reaching Retired says nothing about why. The three reasons are separate
    /// columns, and the roster and the notifications read them.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatusTransition.Kill, true, false, false)]
    [InlineData(CharacterStatusTransition.Exile, false, false, true)]
    [InlineData(CharacterStatusTransition.Leave, false, true, false)]
    public async Task RecordWhyTheCharacterLeft(
        CharacterStatusTransition transition, bool dead, bool left, bool exiled)
    {
        var characterId = Given(CharacterStatus.Active);
        UpdateCharacterEntity? written = null;
        _repository.Update(Arg.Any<UpdateCharacterEntity>())
            .Returns(new Character { Id = characterId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateCharacterEntity>(0);
                written = e;
            });

        await _service.ChangeStatusAsync(characterId, transition);

        written!.IsDead.Should().Be(dead ? true : null);
        written.IsPlayerLeft.Should().Be(left ? true : null);
        written.IsPlayerExiled.Should().Be(exiled ? true : null);
    }

    /// <summary>
    /// Coming back clears all three, or a resurrected character stays marked dead
    /// and cannot be killed again, and a readmitted one stays marked exiled.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatusTransition.Resurrect)]
    [InlineData(CharacterStatusTransition.Readmit)]
    public async Task ClearEveryReasonOnTheWayBack(CharacterStatusTransition transition)
    {
        var characterId = Given(CharacterStatus.Retired);
        UpdateCharacterEntity? written = null;
        _repository.Update(Arg.Any<UpdateCharacterEntity>())
            .Returns(new Character { Id = characterId })
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateCharacterEntity>(0);
                written = e;
            });

        await _service.ChangeStatusAsync(characterId, transition);

        written!.IsDead.Should().Be(false);
        written.IsPlayerLeft.Should().Be(false);
        written.IsPlayerExiled.Should().Be(false);
    }

    public static TheoryData<CharacterStatusTransition, CharacterStatus> IllegalTransitions() => new()
    {
        { CharacterStatusTransition.Accept, CharacterStatus.Active },
        { CharacterStatusTransition.Accept, CharacterStatus.Retired },
        { CharacterStatusTransition.Decline, CharacterStatus.Active },
        { CharacterStatusTransition.Decline, CharacterStatus.Declined },
        { CharacterStatusTransition.Kill, CharacterStatus.Retired },
        { CharacterStatusTransition.Kill, CharacterStatus.UnderReview },
        { CharacterStatusTransition.Exile, CharacterStatus.Retired },
        { CharacterStatusTransition.Leave, CharacterStatus.UnderReview },
        { CharacterStatusTransition.Resurrect, CharacterStatus.Active },
        { CharacterStatusTransition.Readmit, CharacterStatus.Active },
        { CharacterStatusTransition.Readmit, CharacterStatus.UnderReview },
    };

    /// <summary>
    /// A transition that makes no sense from here is the caller's mistake, so 400.
    /// It used to be an unhandled exception, which is 500 with a correlation token
    /// and a line in the error log.
    /// </summary>
    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public async Task RefuseATransitionThatDoesNotStartHere(
        CharacterStatusTransition transition, CharacterStatus from)
    {
        var characterId = Given(from);

        var act = () => _service.ChangeStatusAsync(characterId, transition);

        var thrown = await act.Should().ThrowAsync<HttpException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().Update(Arg.Any<UpdateCharacterEntity>());
    }

    [Fact]
    public async Task AnswerNotFoundForACharacterThatIsNotThere()
    {
        var characterId = Guid.NewGuid();
        _repository.FindCharacter(characterId).Returns((Character?)null);

        var act = () => _service.ChangeStatusAsync(characterId, CharacterStatusTransition.Kill);

        var thrown = await act.Should().ThrowAsync<HttpException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// From Retired the ways back are Resurrect and Readmit, the lead undoing his
    /// own Kill or Exile (the resolver asks which of the two it was). Every other
    /// transition, including any that would bring back what the player took out
    /// of the game, does not start here.
    /// </summary>
    [Fact]
    public async Task LeaveOnlyTheLeadsUndoingsAsTheWaysBackFromRetired()
    {
        foreach (var transition in Enum.GetValues<CharacterStatusTransition>()
                     .Where(t => t is not (CharacterStatusTransition.Resurrect or CharacterStatusTransition.Readmit)))
        {
            var characterId = Given(CharacterStatus.Retired);

            var act = () => _service.ChangeStatusAsync(characterId, transition);

            (await act.Should().ThrowAsync<HttpException>(transition.ToString()))
                .Which.StatusCode.Should().Be(HttpStatusCode.BadRequest, transition.ToString());
        }

        await _repository.DidNotReceive().Update(Arg.Any<UpdateCharacterEntity>());
    }

    /// <summary>
    /// The game's blacklist does not stand between a lead and his own moves: he
    /// accepts the application he declined, or one the entry declined, revives
    /// the character he killed and brings back the one he exiled, whoever the
    /// list holds.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatusTransition.Accept, CharacterStatus.Declined)]
    [InlineData(CharacterStatusTransition.Accept, CharacterStatus.UnderReview)]
    [InlineData(CharacterStatusTransition.Resurrect, CharacterStatus.Retired)]
    [InlineData(CharacterStatusTransition.Readmit, CharacterStatus.Retired)]
    public async Task LetALeadUndoHisOwnMoveForABlacklistedAuthor(
        CharacterStatusTransition transition, CharacterStatus from)
    {
        var authorId = Guid.NewGuid();
        var characterId = Given(from, authorId);
        BlacklistInTheGameOf(characterId, authorId);
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.ChangeStatusAsync(characterId, transition);

        await _repository.Received(1).Update(Arg.Any<UpdateCharacterEntity>());
    }

    /// <summary>
    /// Taking a character out of the game is not what the list closes: a lead
    /// still kills, exiles and declines, and the author still leaves.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatusTransition.Kill, CharacterStatus.Active)]
    [InlineData(CharacterStatusTransition.Exile, CharacterStatus.Active)]
    [InlineData(CharacterStatusTransition.Leave, CharacterStatus.Active)]
    [InlineData(CharacterStatusTransition.Decline, CharacterStatus.UnderReview)]
    public async Task LeaveTheWaysOutOpenForABlacklistedAuthor(
        CharacterStatusTransition transition, CharacterStatus from)
    {
        var characterId = Given(from, _currentUserId);
        BlacklistInTheGameOf(characterId, _currentUserId);
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });

        await _service.ChangeStatusAsync(characterId, transition);

        await _repository.Received(1).Update(Arg.Any<UpdateCharacterEntity>());
    }

    /// <summary>
    /// A character in the given status, existing.
    /// </summary>
    private Guid Given(CharacterStatus status, Guid? authorId = null)
    {
        var characterId = Guid.NewGuid();
        _repository.FindCharacter(characterId).Returns(new Character { Id = characterId });
        _repository.GetForUpdate(characterId).Returns(new CharacterToUpdate
        {
            Id = characterId,
            Status = status,
            GameId = Guid.NewGuid(),
            AuthorId = authorId ?? Guid.NewGuid(),
            GameAssistantIds = Array.Empty<Guid>(),
        });
        return characterId;
    }

    /// <summary>
    /// The game of this character, with the user on its blacklist.
    /// </summary>
    private void BlacklistInTheGameOf(Guid characterId, Guid userId)
    {
        var gameId = _repository.GetForUpdate(characterId).Result!.GameId;
        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            BlacklistedUsers = [new BlacklistedUser { UserId = userId }]
        });
    }

    /// <summary>
    /// Losing the last active character leaves the PLAYER with the game as a
    /// reader. The subscription was written for whoever sent the request while the
    /// "any characters left" question was asked about the character's author, so
    /// the lead subscribed himself to his own game and the player lost it.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatusTransition.Kill)]
    [InlineData(CharacterStatusTransition.Exile)]
    [InlineData(CharacterStatusTransition.Leave)]
    public async Task SubscribeThePlayerAndNotTheCaller(CharacterStatusTransition transition)
    {
        var characterId = Given(CharacterStatus.Active);
        var character = (await _repository.GetForUpdate(characterId))!;
        _repository.Update(Arg.Any<UpdateCharacterEntity>()).Returns(new Character { Id = characterId });
        _repository
            .HasOtherActiveCharacters(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(false);

        await _service.ChangeStatusAsync(characterId, transition);

        await _subscriptions.Received(1).SubscribeUserAsync(character.GameId, character.UserId, Arg.Any<CancellationToken>());
    }
}
