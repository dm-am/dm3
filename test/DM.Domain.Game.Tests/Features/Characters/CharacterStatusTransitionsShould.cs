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
using FluentAssertions;
using FluentValidation;
using Moq;
using Xunit;

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
/// leaves, and only the player comes back.
/// </remarks>
public class CharacterStatusTransitionsShould : UnitTestBase
{
    private readonly Mock<ICharacterRepository> _repository;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IEventProducer> _producer;
    private readonly ICharacterService _service;

    public CharacterStatusTransitionsShould()
    {
        var createValidator = Mock<IValidator<CreateCharacter>>();
        var updateValidator = Mock<IValidator<UpdateCharacter>>();
        _repository = Mock<ICharacterRepository>();
        _intentionManager = Mock<IIntentionManager>();
        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identities.User(Guid.NewGuid()));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new CharacterService(
            createValidator.Object,
            updateValidator.Object,
            Mock<IGameService>().Object,
            _intentionManager.Object,
            _repository.Object,
            Mock<ICharacterAttributeValueFiller>().Object,
            Mock<IUnreadCountersRepository>().Object,
            Mock<IGameSubscriptionService>().Object,
            _producer.Object,
            identityProvider.Object,
            Mock<IGuidFactory>().Object,
            dateTimeProvider.Object);
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
        { CharacterStatusTransition.Return, CharacterStatus.Retired, CharacterIntention.Return,
            CharacterStatus.Active, EventType.StatusCharacterReturned },
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public async Task AskTheRightPermissionAndWriteTheRightStatus(
        CharacterStatusTransition transition, CharacterStatus from, CharacterIntention intention,
        CharacterStatus to, EventType raised)
    {
        var characterId = Given(from);
        UpdateCharacterEntity? written = null;
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .Callback<UpdateCharacterEntity>(e => written = e)
            .ReturnsAsync(new Character { Id = characterId });

        await _service.ChangeStatusAsync(characterId, transition);

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(intention, It.IsAny<CharacterToUpdate>()), Times.Once);
        written!.Status.Should().Be(to);
        _producer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(raised)), characterId), Times.Once);
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
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .Callback<UpdateCharacterEntity>(e => written = e)
            .ReturnsAsync(new Character { Id = characterId });

        await _service.ChangeStatusAsync(characterId, transition);

        written!.IsDead.Should().Be(dead ? true : null);
        written.IsPlayerLeft.Should().Be(left ? true : null);
        written.IsPlayerExiled.Should().Be(exiled ? true : null);
    }

    /// <summary>
    /// Coming back clears all three, or a resurrected character stays marked dead
    /// and cannot be killed again.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatusTransition.Resurrect)]
    [InlineData(CharacterStatusTransition.Return)]
    public async Task ClearEveryReasonOnTheWayBack(CharacterStatusTransition transition)
    {
        var characterId = Given(CharacterStatus.Retired);
        UpdateCharacterEntity? written = null;
        _repository.Setup(r => r.Update(It.IsAny<UpdateCharacterEntity>()))
            .Callback<UpdateCharacterEntity>(e => written = e)
            .ReturnsAsync(new Character { Id = characterId });

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
        { CharacterStatusTransition.Return, CharacterStatus.Active },
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
        _repository.Verify(r => r.Update(It.IsAny<UpdateCharacterEntity>()), Times.Never);
    }

    [Fact]
    public async Task AnswerNotFoundForACharacterThatIsNotThere()
    {
        var characterId = Guid.NewGuid();
        _repository.Setup(r => r.FindCharacter(characterId)).ReturnsAsync((Character?)null);

        var act = () => _service.ChangeStatusAsync(characterId, CharacterStatusTransition.Kill);

        var thrown = await act.Should().ThrowAsync<HttpException>();
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A character in the given status, existing.
    /// </summary>
    private Guid Given(CharacterStatus status)
    {
        var characterId = Guid.NewGuid();
        _repository.Setup(r => r.FindCharacter(characterId))
            .ReturnsAsync(new Character { Id = characterId });
        _repository.Setup(r => r.GetForUpdate(characterId))
            .ReturnsAsync(new CharacterToUpdate
            {
                Id = characterId,
                Status = status,
                GameId = Guid.NewGuid(),
                AuthorId = Guid.NewGuid(),
                GameAssistantIds = Array.Empty<Guid>(),
            });
        return characterId;
    }
}
