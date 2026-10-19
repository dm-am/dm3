using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Subscriptions;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Core.Dto;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Invitations;

public class BlogInvitationServiceShould : UnitTestBase
{
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IBlogInvitationRepository repository;
    private readonly IIntentionManager intentionManager;
    private readonly IBlogService blogService;
    private readonly IBlogSubscriptionService subscriptionService;
    private readonly IUserLookupService userLookupService;
    private readonly IUserBlacklistChecker userBlacklistChecker;
    private readonly IEventProducer eventProducer;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly BlogInvitationService service;

    public BlogInvitationServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        repository = Mock<IBlogInvitationRepository>();
        intentionManager = Mock<IIntentionManager>();
        blogService = Mock<IBlogService>();
        subscriptionService = Mock<IBlogSubscriptionService>();
        userLookupService = Mock<IUserLookupService>();
        userBlacklistChecker = Mock<IUserBlacklistChecker>();
        eventProducer = Mock<IEventProducer>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        identityProvider.Current.Returns(Identity.Guest());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        userBlacklistChecker.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), default).Returns(false);

        service = new BlogInvitationService(
            identityProvider,
            guidFactory,
            repository,
            intentionManager,
            blogService,
            subscriptionService,
            userLookupService,
            userBlacklistChecker,
            eventProducer,
            dateTimeProvider);
    }

    [Fact]
    public async Task AuthorizeAndAnnounceAnAssistantInvitation()
    {
        var blogId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Title = "Test Blog", BlacklistedUserIds = new HashSet<Guid>() };
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        guidFactory.Create().Returns(tokenId);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        repository.FindInvitations(blogId, invitedUserId, TokenType.BlogAssistantInvitation, default)
            .Returns(new List<Guid>());

        await service.InviteAssistant(blogId, "invitee");

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.InviteAssistant, blog);
        await eventProducer.Received(1).SendAsync(EventType.BlogInvitationCreated, tokenId);
    }

    [Fact]
    public async Task CarryTheTitleOfTheBlogReadOnce()
    {
        var blogId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Title = "Test Blog", BlacklistedUserIds = new HashSet<Guid>() };

        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        repository.FindInvitations(blogId, invitedUserId, TokenType.BlogAssistantInvitation, default)
            .Returns(new List<Guid>());

        var invitation = await service.InviteAssistant(blogId, "invitee");

        invitation.BlogTitle.Should().Be("Test Blog");
        await blogService.Received(1).GetUnfiltered(blogId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The blog's list closes comments only. Inviting somebody it holds is the
    /// owner's own decision, and the invitation is sent.
    /// </summary>
    [Fact]
    public async Task InviteAUserTheBlogBlacklisted()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { invitedUserId } };
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        repository.FindInvitations(blogId, invitedUserId, TokenType.BlogReaderInvitation, default)
            .Returns(new List<Guid>());

        await service.InviteReader(blogId, "invitee");

        await repository.Received(1).InvalidateAndCreate(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Is<CreateBlogInvitationEntity>(e => e.UserId == invitedUserId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An invitation reaches its addressee signed with the inviter's name, so it
    /// is not created for somebody who keeps the inviter on their personal list -
    /// and the refusal names nobody and gives no reason.
    /// </summary>
    [Fact]
    public async Task RefuseToInviteSomebodyWhoBlockedTheInviter()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var identity = Identities.User(userId, "testuser");

        identityProvider.Current.Returns(identity);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        userBlacklistChecker.IsBlockedAsync(invitedUserId, userId, default).Returns(true);

        var act = async () => await service.InviteAssistant(blogId, "invitee");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.CannotInviteThisUser);
        await repository.DidNotReceive().InvalidateAndCreate(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<CreateBlogInvitationEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The inviter's own list restricts the people on it, not its owner: inviting
    /// somebody one blocked is that owner's decision alone.
    /// </summary>
    [Fact]
    public async Task InviteSomebodyTheInviterBlocked()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };

        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        userBlacklistChecker.IsBlockedAsync(userId, invitedUserId, default).Returns(true);

        await service.InviteAssistant(blogId, "invitee");

        await repository.Received(1).InvalidateAndCreate(
            Arg.Any<IEnumerable<Guid>>(), Arg.Any<CreateBlogInvitationEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenAcceptingExpiredInvitation()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, "testuser");
        var expiredTime = DateTimeOffset.UtcNow.AddDays(-1);
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            InvitedUser = new GeneralUser { UserId = userId },
            TargetRole = BlogRole.Assistant,
            ExpiresUtc = expiredTime
        };

        identityProvider.Current.Returns(identity);
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        repository.GetInvitation(tokenId, default).Returns(invitation);

        var act = async () => await service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Gone && e.Message.Contains("истек"));
    }

    [Fact]
    public async Task ThrowWhenAcceptingInvitationForAnotherUser()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var identity = Identities.User(userId, "testuser");
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            InvitedUser = new GeneralUser { UserId = otherUserId },
            TargetRole = BlogRole.Assistant,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        identityProvider.Current.Returns(identity);
        repository.GetInvitation(tokenId, default).Returns(invitation);

        var act = async () => await service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A reader invitation is redeemed past the public subscription door.
    /// </summary>
    /// <remarks>
    /// BlogUserService.Subscribe refuses a blog whose drafts are private, the very blog an invitation exists for.
    /// </remarks>
    [Fact]
    public async Task SubscribeTheReaderWithoutThePublicDoor()
    {
        var tokenId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            BlogId = blogId,
            InvitedUser = new GeneralUser { UserId = userId },
            TargetRole = BlogRole.Reader,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        identityProvider.Current.Returns(Identities.User(userId, "testuser"));
        repository.GetInvitation(tokenId, default).Returns(invitation);

        await service.AcceptInvitation(tokenId);

        await subscriptionService.Received(1).SubscribeAsync(blogId, default);
    }

    [Fact]
    public async Task AuthorizeCancelInvitationAction()
    {
        var tokenId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, "testuser");
        var blog = new BlogDto { Id = blogId };
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            BlogId = blogId,
            TargetRole = BlogRole.Assistant
        };

        identityProvider.Current.Returns(identity);
        repository.GetInvitation(tokenId, default).Returns(invitation);
        blogService.GetUnfiltered(blogId, default).Returns(blog);

        await service.CancelInvitation(tokenId);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CancelInvitation, blog);
    }
}
