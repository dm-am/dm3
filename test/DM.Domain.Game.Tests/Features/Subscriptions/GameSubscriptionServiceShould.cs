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
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Subscriptions;

public class GameSubscriptionServiceShould : UnitTestBase
{
    private readonly Mock<ISubscriptionRepository> _repository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly GameSubscriptionService _service;
    private readonly Guid _currentUserId;

    public GameSubscriptionServiceShould()
    {
        _repository = Mock<ISubscriptionRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();

        _currentUserId = Guid.NewGuid();
        var identity = Identity.User(_currentUserId, "testuser");
        _identityProvider.Setup(p => p.Current).Returns(identity);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new GameSubscriptionService(
            _repository.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            guidFactory.Object,
            dateTimeProvider.Object);
    }

    [Fact]
    public async Task CreateNewSubscription()
    {
        var gameId = Guid.NewGuid();

        _repository.Setup(r => r.FindAsync(_currentUserId, SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreateSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription());

        await _service.SubscribeAsync(gameId);

        _repository.Verify(r => r.CreateAsync(
            It.Is<CreateSubscription>(s =>
                s.SubscriberId == _currentUserId &&
                s.TargetType == SubscriptionTargetType.Game &&
                s.TargetId == gameId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnExistingSubscription()
    {
        var gameId = Guid.NewGuid();
        var existingSubscription = new Subscription();

        _repository.Setup(r => r.FindAsync(_currentUserId, SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubscription);

        var result = await _service.SubscribeAsync(gameId);

        result.Should().Be(existingSubscription);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CreateSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnsubscribeFromGame()
    {
        var gameId = Guid.NewGuid();

        _repository.Setup(r => r.DeleteAsync(_currentUserId, SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.UnsubscribeAsync(gameId);

        _repository.Verify(r => r.DeleteAsync(_currentUserId, SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGameReaders()
    {
        var gameId = Guid.NewGuid();
        var subscriberIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var users = subscriberIds.Select(id => new GeneralUser { UserId = id }).ToList();

        _repository.Setup(r => r.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriberIds);

        foreach (var user in users)
        {
            _userLookupService.Setup(s => s.GetAsync(user.UserId))
                .ReturnsAsync(user);
        }

        var result = await _service.GetSubscribersAsync(gameId);

        result.Should().HaveCount(2);
        result.Should().Contain(users);
    }

    [Fact]
    public async Task ReturnEmptyListWhenNoReaders()
    {
        var gameId = Guid.NewGuid();

        _repository.Setup(r => r.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Guid>());

        var result = await _service.GetSubscribersAsync(gameId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckIfUserIsSubscribed()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repository.Setup(r => r.FindAsync(userId, SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription());

        var result = await _service.IsSubscribedAsync(userId, gameId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIfUserIsNotSubscribed()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repository.Setup(r => r.FindAsync(userId, SubscriptionTargetType.Game, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var result = await _service.IsSubscribedAsync(userId, gameId);

        result.Should().BeFalse();
    }
}
