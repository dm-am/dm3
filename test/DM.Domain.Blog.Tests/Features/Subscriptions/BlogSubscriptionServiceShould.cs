using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Subscriptions;

public class BlogSubscriptionServiceShould : UnitTestBase
{
    private readonly ISubscriptionRepository repository;
    private readonly IUserLookupService userLookupService;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly BlogSubscriptionService service;

    public BlogSubscriptionServiceShould()
    {
        repository = Mock<ISubscriptionRepository>();
        userLookupService = Mock<IUserLookupService>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        identityProvider.Current.Returns(Identity.Guest());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new BlogSubscriptionService(
            repository,
            userLookupService,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    [Fact]
    public async Task CreateSubscriptionWhenNotAlreadySubscribed()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var identity = Identities.User(userId, "testuser");
        var createdSubscription = new Subscription { Id = subscriptionId };

        identityProvider.Current.Returns(identity);
        guidFactory.Create().Returns(subscriptionId);
        repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default).Returns((Subscription?)null);
        repository.CreateAsync(Arg.Any<CreateSubscription>(), default).Returns(createdSubscription);

        var result = await service.SubscribeAsync(blogId);

        result.Id.Should().Be(subscriptionId);
        await repository.Received(1).CreateAsync(
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
        var identity = Identities.User(userId, "testuser");
        var existingSubscription = new Subscription { Id = subscriptionId };

        identityProvider.Current.Returns(identity);
        repository.FindAsync(userId, SubscriptionTargetType.Blog, blogId, default).Returns(existingSubscription);

        var result = await service.SubscribeAsync(blogId);

        result.Should().Be(existingSubscription);
        await repository.DidNotReceive().CreateAsync(
            Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSubscriptionOnUnsubscribe()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);

        await service.UnsubscribeAsync(blogId);

        await repository.Received(1).DeleteAsync(userId, SubscriptionTargetType.Blog, blogId, default);
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

        repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, default).Returns(subscriberIds);

        // Asked for all of them at once. One call per reader made the page cost
        // as much as it had readers, and the single-user form throws on a user
        // who is no longer there, so one removed reader answered the whole blog
        // with 404.
        userLookupService
            .GetReferencesAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(subscriberIds)))
            .Returns(new[] { user1, user2 });

        var result = await service.GetReadersAsync(blogId);

        result.Should().HaveCount(2);
        result.Should().Contain(u => u.UserId == userId1);
        result.Should().Contain(u => u.UserId == userId2);
        await userLookupService.DidNotReceive().GetAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ReturnEmptyWhenNoReaders()
    {
        var blogId = Guid.NewGuid();

        repository.GetTargetSubscriberIdsAsync(SubscriptionTargetType.Blog, blogId, default).Returns(new List<Guid>());

        var result = await service.GetReadersAsync(blogId);

        result.Should().BeEmpty();
    }
}
