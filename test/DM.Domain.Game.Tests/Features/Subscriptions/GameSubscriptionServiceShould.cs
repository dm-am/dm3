using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Subscriptions;

public class GameSubscriptionServiceShould : UnitTestBase
{
    private readonly ISubscriptionRepository repository;
    private readonly IUserLookupService userLookupService;
    private readonly IIdentityProvider identityProvider;
    private readonly GameSubscriptionService service;
    private readonly Guid currentUserId;

    public GameSubscriptionServiceShould()
    {
        repository = Mock<ISubscriptionRepository>();
        userLookupService = Mock<IUserLookupService>();
        identityProvider = Mock<IIdentityProvider>();

        currentUserId = Guid.NewGuid();
        var identity = Identities.User(currentUserId, "testuser");
        identityProvider.Current.Returns(identity);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new GameSubscriptionService(
            repository,
            userLookupService,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    [Fact]
    public async Task CreateNewSubscription()
    {
        var gameId = Guid.NewGuid();

        repository.FindAsync(currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);
        repository.CreateAsync(Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>())
            .Returns(new Subscription());

        await service.SubscribeAsync(gameId);

        await repository.Received(1).CreateAsync(
            Arg.Is<CreateSubscription>(s =>
                s.SubscriberId == currentUserId &&
                s.TargetType == SubscriptionTargetType.Game &&
                s.TargetId == gameId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnExistingSubscription()
    {
        var gameId = Guid.NewGuid();
        var existingSubscription = new Subscription();

        repository.FindAsync(currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(existingSubscription);

        var result = await service.SubscribeAsync(gameId);

        result.Should().Be(existingSubscription);
        await repository.DidNotReceive().CreateAsync(Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeFromGame()
    {
        var gameId = Guid.NewGuid();

        repository.DeleteAsync(currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await service.UnsubscribeAsync(gameId);

        await repository.Received(1).DeleteAsync(currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetGameReaders()
    {
        var gameId = Guid.NewGuid();
        var subscriberIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var users = subscriberIds.Select(id => new UserReference { UserId = id }).ToList();

        repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(subscriberIds);

        // Asked for all of them at once: the single-user form throws on a removed user.
        userLookupService
            .GetReferencesAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(subscriberIds))).Returns(users);

        var result = await service.GetSubscribersAsync(gameId);

        result.Should().HaveCount(2);
        result.Should().Contain(users);
        await userLookupService.DidNotReceive().GetAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ReturnEmptyListWhenNoReaders()
    {
        var gameId = Guid.NewGuid();

        repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Guid>());

        var result = await service.GetSubscribersAsync(gameId);

        result.Should().BeEmpty();
    }
}
