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
using DM.Testing;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Subscriptions;

public class BlogSubscriptionServiceShould : UnitTestBase
{
    private readonly ISubscriptionRepository _repository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBlogBlacklistRepository _blacklistRepository;
    private readonly BlogSubscriptionService _service;

    public BlogSubscriptionServiceShould()
    {
        _repository = Mock<ISubscriptionRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _blacklistRepository = Mock<IBlogBlacklistRepository>();

        _identityProvider.Current.Returns(Identity.Guest());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new BlogSubscriptionService(
            _repository,
            _userLookupService,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            // The real guard over the mocked store: the rule under test is the
            // guard's, and the generic subscription endpoint asks the same
            // object, so a stub here would test a copy of it that no caller uses.
            new BlogSubscriptionGuard(_blacklistRepository));
    }

    [Fact]
    public async Task RefuseToSubscribeAUserTheBlogBlacklisted()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(userId));
        _blacklistRepository
            .IsBlocked(blogId, userId, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.SubscribeAsync(blogId);

        // Reading the blog is open to them and stays open. Joining its list of
        // readers is the write the blacklist refuses, and the entry that put them
        // on the list dropped the subscription they had.
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSubscriptionWhenNotAlreadySubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var identity = AuthenticatedIdentities.Of(userId);
        var createdSubscription = new Subscription { Id = subscriptionId };

        _identityProvider.Current.Returns(identity);
        _guidFactory.Create().Returns(subscriptionId);
        _repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default).Returns((Subscription?)null);
        _repository.CreateAsync(Arg.Any<CreateSubscription>(), default).Returns(createdSubscription);

        var result = await _service.SubscribeAsync(blogId);

        result.Id.Should().Be(subscriptionId);
        await _repository.Received(1).CreateAsync(
            Arg.Is<CreateSubscription>(s =>
                s.SubscriberId == userId &&
                s.TargetType == SubscriptionTargetType.Blog &&
                s.TargetId == blogId),
            default);
    }

    [Fact]
    public async Task ReturnExistingSubscriptionWhenAlreadySubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var identity = AuthenticatedIdentities.Of(userId);
        var existingSubscription = new Subscription { Id = subscriptionId };

        _identityProvider.Current.Returns(identity);
        _repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default).Returns(existingSubscription);

        var result = await _service.SubscribeAsync(blogId);

        result.Should().Be(existingSubscription);
        await _repository.DidNotReceive().CreateAsync(
            Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSubscriptionOnUnsubscribe()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);

        await _service.UnsubscribeAsync(blogId);

        await _repository.Received(1).DeleteAsync(userId, SubscriptionTargetType.Blog, blogId, default);
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

        _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, default).Returns(subscriberIds);

        // Asked for all of them at once. One call per reader made the page cost
        // as much as it had readers, and the single-user form throws on a user
        // who is no longer there, so one removed reader answered the whole blog
        // with 404.
        _userLookupService
            .GetReferencesAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(subscriberIds)))
            .Returns(new[] { user1, user2 });

        var result = await _service.GetReadersAsync(blogId);

        result.Should().HaveCount(2);
        result.Should().Contain(u => u.UserId == userId1);
        result.Should().Contain(u => u.UserId == userId2);
        await _userLookupService.DidNotReceive().GetAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ReturnEmptyWhenNoReaders()
    {
        var blogId = Guid.NewGuid();

        _repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, default).Returns(new List<Guid>());

        var result = await _service.GetReadersAsync(blogId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnTrueWhenUserIsSubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscription = new Subscription { Id = Guid.NewGuid() };

        _repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default).Returns(subscription);

        var result = await _service.IsSubscribedAsync(userId, blogId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnFalseWhenUserIsNotSubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default).Returns((Subscription?)null);

        var result = await _service.IsSubscribedAsync(userId, blogId);

        result.Should().BeFalse();
    }

}
