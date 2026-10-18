using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Subscriptions;

public class GameSubscriptionServiceShould : UnitTestBase
{
    private readonly ISubscriptionRepository _repository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly GameSubscriptionService _service;
    private readonly Guid _currentUserId;

    public GameSubscriptionServiceShould()
    {
        _repository = Mock<ISubscriptionRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();

        _currentUserId = Guid.NewGuid();
        var identity = Identities.User(_currentUserId, "testuser");
        _identityProvider.Current.Returns(identity);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new GameSubscriptionService(
            _repository,
            _userLookupService,
            _identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    /// <summary>
    /// The game's blacklist does not stand between anybody and a subscription.
    /// </summary>
    /// <remarks>
    /// It closes writing, and following the game is reading. The service used to
    /// hold the list and refuse the button with "Вы в черном списке этой игры";
    /// now it cannot even reach it, which is what this asserts: the rule lives
    /// with the writes it closes (GameRoleExtensions.IsBlacklisted), and the
    /// behaviour end to end is in BlacklistRuleShould.
    /// </remarks>
    [Fact]
    public void KnowNothingOfTheBlacklist()
    {
        typeof(GameSubscriptionService).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Should().NotContain([typeof(IGameBlacklistRepository), typeof(IGameService)]);
    }

    [Fact]
    public async Task CreateNewSubscription()
    {
        var gameId = Guid.NewGuid();

        _repository.FindAsync(_currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);
        _repository.CreateAsync(Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>())
            .Returns(new Subscription());

        await _service.SubscribeAsync(gameId);

        await _repository.Received(1).CreateAsync(
            Arg.Is<CreateSubscription>(s =>
                s.SubscriberId == _currentUserId &&
                s.TargetType == SubscriptionTargetType.Game &&
                s.TargetId == gameId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnExistingSubscription()
    {
        var gameId = Guid.NewGuid();
        var existingSubscription = new Subscription();

        _repository.FindAsync(_currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(existingSubscription);

        var result = await _service.SubscribeAsync(gameId);

        result.Should().Be(existingSubscription);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeFromGame()
    {
        var gameId = Guid.NewGuid();

        _repository.DeleteAsync(_currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _service.UnsubscribeAsync(gameId);

        await _repository.Received(1).DeleteAsync(_currentUserId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetGameReaders()
    {
        var gameId = Guid.NewGuid();
        var subscriberIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var users = subscriberIds.Select(id => new UserReference { UserId = id }).ToList();

        _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(subscriberIds);

        // Asked for all of them at once. One call per subscriber made the page
        // cost as much as it had readers, and the single-user form throws on a
        // user who is no longer there, so one removed reader answered the whole
        // game page with 404.
        _userLookupService
            .GetReferencesAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(subscriberIds))).Returns(users);

        var result = await _service.GetSubscribersAsync(gameId);

        result.Should().HaveCount(2);
        result.Should().Contain(users);
        await _userLookupService.DidNotReceive().GetAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ReturnEmptyListWhenNoReaders()
    {
        var gameId = Guid.NewGuid();

        _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Guid>());

        var result = await _service.GetSubscribersAsync(gameId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckIfUserIsSubscribed()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repository.FindAsync(userId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns(new Subscription());

        var result = await _service.IsSubscribedAsync(userId, gameId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIfUserIsNotSubscribed()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repository.FindAsync(userId, SubscriptionTargetType.Game, gameId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        var result = await _service.IsSubscribedAsync(userId, gameId);

        result.Should().BeFalse();
    }
}
