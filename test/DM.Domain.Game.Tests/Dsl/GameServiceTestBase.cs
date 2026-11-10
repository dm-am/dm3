using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DM.Domain.Game.Tests.Dsl;

public abstract class GameServiceTestBase : UnitTestBase
{
    protected readonly IIntentionManager intentionManager;
    protected readonly IGameTagResolver tagResolver;
    protected readonly IGameRepository repository;
    protected readonly IUnreadCountersRepository unreadCountersRepository;
    protected readonly IGameSubscriptionService subscriptionService;
    protected readonly IEventProducer producer;
    protected readonly IIdentityProvider identityProvider;
    protected readonly IGuidFactory guidFactory;
    protected readonly IDateTimeProvider dateTimeProvider;
    protected readonly ICache cache;
    protected readonly IUserBlacklistChecker blacklistChecker = Mock<IUserBlacklistChecker>();
    private protected readonly GameService service;
    protected readonly Guid currentUserId;
    protected readonly AuthenticatedUser author;

    protected GameServiceTestBase()
    {
        tagResolver = Mock<IGameTagResolver>();
        tagResolver.ResolveTagIds(Arg.Any<IEnumerable<int>?>()).Returns(Array.Empty<Guid>());

        intentionManager = Mock<IIntentionManager>();

        var gameSchemaRepository = Mock<IGameAttributeSchemaRepository>();
        gameSchemaRepository.Get(Arg.Any<Guid>()).Returns(new GameAttributeSchema());

        repository = Mock<IGameRepository>();

        var assistantRepository = Mock<IGameAssistantRepository>();

        var invitationService = Mock<IGameInvitationService>();

        currentUserId = Guid.NewGuid();
        identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(currentUserId, UserRole.RegularUser);
        author = identity.User;
        identityProvider.Current.Returns(identity);

        var gameBlacklistRepository = Mock<IGameBlacklistRepository>();

        unreadCountersRepository = ZeroUnreadCounters();

        subscriptionService = Mock<IGameSubscriptionService>();

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);
        producer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        cache = Mock<ICache>();

        var logger = Mock<ILogger<GameService>>();

        service = new GameService(
            PassingValidator<GamesQuery>(),
            PassingValidator<UpdateGame>(),
            PassingValidator<CreateGame>(),
            tagResolver,
            intentionManager,
            gameSchemaRepository,
            repository,
            assistantRepository,
            invitationService,
            identityProvider,
            gameBlacklistRepository,
            blacklistChecker,
            unreadCountersRepository,
            subscriptionService,
            dateTimeProvider,
            guidFactory,
            producer,
            cache,
            logger);
    }

    private static IValidator<T> PassingValidator<T>()
    {
        var validator = Mock<IValidator<T>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<T>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        return validator;
    }

    private static IUnreadCountersRepository ZeroUnreadCounters()
    {
        var counters = Mock<IUnreadCountersRepository>();
        counters.SelectByEntitiesAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid[]>())
            .Returns(ci =>
            {
                var ids = ci.ArgAt<Guid[]>(2);
                return ids.ToDictionary(id => id, _ => 0) as IDictionary<Guid, int>;
            });
        return counters;
    }
}
