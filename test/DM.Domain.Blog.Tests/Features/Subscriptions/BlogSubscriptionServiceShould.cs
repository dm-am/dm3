using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Domain.Account.Features.Authentication;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Subscriptions;

public class BlogSubscriptionServiceShould : UnitTestBase
{
    private readonly Mock<ISubscriptionRepository> _repository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IBlogBlacklistRepository> _blacklistRepository;
    private readonly BlogSubscriptionService _service;

    public BlogSubscriptionServiceShould()
    {
        _repository = Mock<ISubscriptionRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _blacklistRepository = Mock<IBlogBlacklistRepository>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new BlogSubscriptionService(
            _repository.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            // The real guard over the mocked store: the rule under test is the
            // guard's, and the generic subscription endpoint asks the same
            // object, so a stub here would test a copy of it that no caller uses.
            new BlogSubscriptionGuard(_blacklistRepository.Object));
    }

    [Fact]
    public async Task RefuseToSubscribeAUserTheBlogBlacklisted()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _identityProvider.Setup(p => p.Current).Returns(CreateAuthenticatedIdentity(userId));
        _blacklistRepository
            .Setup(r => r.IsBlocked(blogId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = async () => await _service.SubscribeAsync(blogId);

        // Reading the blog is open to them and stays open. Joining its list of
        // readers is the write the blacklist refuses, and the entry that put them
        // on the list dropped the subscription they had.
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        _repository.Verify(
            r => r.CreateAsync(It.IsAny<CreateSubscription>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSubscriptionWhenNotAlreadySubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var identity = CreateAuthenticatedIdentity(userId);
        var createdSubscription = new Subscription { Id = subscriptionId };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _guidFactory.Setup(f => f.Create()).Returns(subscriptionId);
        _repository.Setup(r => r.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default))
            .ReturnsAsync((Subscription?)null);
        _repository.Setup(r => r.CreateAsync(It.IsAny<CreateSubscription>(), default))
            .ReturnsAsync(createdSubscription);

        var result = await _service.SubscribeAsync(blogId);

        result.Id.Should().Be(subscriptionId);
        _repository.Verify(r => r.CreateAsync(
            It.Is<CreateSubscription>(s =>
                s.SubscriberId == userId &&
                s.TargetType == SubscriptionTargetType.Blog &&
                s.TargetId == blogId),
            default), Times.Once);
    }

    [Fact]
    public async Task ReturnExistingSubscriptionWhenAlreadySubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var identity = CreateAuthenticatedIdentity(userId);
        var existingSubscription = new Subscription { Id = subscriptionId };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default))
            .ReturnsAsync(existingSubscription);

        var result = await _service.SubscribeAsync(blogId);

        result.Should().Be(existingSubscription);
        _repository.Verify(r => r.CreateAsync(It.IsAny<CreateSubscription>(), default), Times.Never);
    }

    [Fact]
    public async Task DeleteSubscriptionOnUnsubscribe()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);

        await _service.UnsubscribeAsync(blogId);

        _repository.Verify(r => r.DeleteAsync(userId, SubscriptionTargetType.Blog, blogId, default), Times.Once);
    }

    [Fact]
    public async Task ReturnReadersForBlog()
    {
        var blogId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var subscriberIds = new List<Guid> { userId1, userId2 };
        var user1 = new UserReference { UserId = userId1, Username = "user1" };
        var user2 = new UserReference { UserId = userId2, Username = "user2" };

        _repository.Setup(r => r.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, default))
            .ReturnsAsync(subscriberIds);

        // Asked for all of them at once. One call per reader made the page cost
        // as much as it had readers, and the single-user form throws on a user
        // who is no longer there, so one removed reader answered the whole blog
        // with 410 Gone.
        _userLookupService
            .Setup(s => s.GetReferencesAsync(It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(subscriberIds))))
            .ReturnsAsync(new[] { user1, user2 });

        var result = await _service.GetReadersAsync(blogId);

        result.Should().HaveCount(2);
        result.Should().Contain(u => u.UserId == userId1);
        result.Should().Contain(u => u.UserId == userId2);
        _userLookupService.Verify(s => s.GetAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ReturnEmptyWhenNoReaders()
    {
        var blogId = Guid.NewGuid();

        _repository.Setup(r => r.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, default))
            .ReturnsAsync(new List<Guid>());

        var result = await _service.GetReadersAsync(blogId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnTrueWhenUserIsSubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscription = new Subscription { Id = Guid.NewGuid() };

        _repository.Setup(r => r.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default))
            .ReturnsAsync(subscription);

        var result = await _service.IsSubscribedAsync(userId, blogId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnFalseWhenUserIsNotSubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repository.Setup(r => r.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default))
            .ReturnsAsync((Subscription?)null);

        var result = await _service.IsSubscribedAsync(userId, blogId);

        result.Should().BeFalse();
    }

    private static IIdentity CreateAuthenticatedIdentity(Guid userId)
    {
        var user = new AuthenticatedUser { UserId = userId, Username = "testuser" };
        var session = new Session();
        return Identity.Success(user, session, UserSettings.Default, "token");
    }
}
