using System;
using System.Threading;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace DM.Domain.Game.Tests.Dsl;

/// <summary>
/// The character service over substitutes: validators that pass, a signed-in
/// caller, and tag names kept as typed unless a test says otherwise.
/// </summary>
public abstract class CharacterServiceTestBase : UnitTestBase
{
    protected readonly Guid currentUserId = Guid.NewGuid();
    protected readonly IGameService gameService = Mock<IGameService>();
    protected readonly IIntentionManager intentionManager = Mock<IIntentionManager>();
    protected readonly ICharacterRepository repository = Mock<ICharacterRepository>();
    protected readonly IGameSubscriptionService subscriptions = Mock<IGameSubscriptionService>();
    protected readonly IEventProducer producer = Mock<IEventProducer>();
    protected readonly IIdentityProvider identityProvider = Mock<IIdentityProvider>();
    private protected readonly CharacterService service;

    protected CharacterServiceTestBase()
    {
        var createValidator = Mock<IValidator<CreateCharacter>>();
        createValidator.ValidateAsync(Arg.Any<ValidationContext<CreateCharacter>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var updateValidator = Mock<IValidator<UpdateCharacter>>();
        updateValidator.ValidateAsync(Arg.Any<ValidationContext<UpdateCharacter>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        identityProvider.Current.Returns(Identities.User(currentUserId));
        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new CharacterService(
            createValidator,
            updateValidator,
            gameService,
            intentionManager,
            repository,
            Mock<ICharacterAttributeValueFiller>(),
            Mock<IUnreadCountersRepository>(),
            subscriptions,
            producer,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }
}
