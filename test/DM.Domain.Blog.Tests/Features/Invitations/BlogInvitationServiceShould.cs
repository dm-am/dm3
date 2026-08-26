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
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Invitations;

public class BlogInvitationServiceShould : UnitTestBase
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IBlogInvitationRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IBlogService _blogService;
    private readonly IBlogSubscriptionService _subscriptionService;
    private readonly IUserLookupService _userLookupService;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly BlogInvitationService _service;

    public BlogInvitationServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _repository = Mock<IBlogInvitationRepository>();
        _intentionManager = Mock<IIntentionManager>();
        _blogService = Mock<IBlogService>();
        _subscriptionService = Mock<IBlogSubscriptionService>();
        _userLookupService = Mock<IUserLookupService>();
        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _eventProducer = Mock<IEventProducer>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _identityProvider.Current.Returns(Identity.Guest());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        _userBlacklistChecker.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), default).Returns(false);

        _service = new BlogInvitationService(
            _identityProvider,
            _guidFactory,
            _repository,
            _intentionManager,
            _blogService,
            _subscriptionService,
            _userLookupService,
            _userBlacklistChecker,
            _eventProducer,
            _dateTimeProvider);
    }

    [Fact]
    public async Task AuthorizeInviteAssistantAction()
    {
        var blogId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Title = "Test Blog", BlacklistedUserIds = new HashSet<Guid>() };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _guidFactory.Create().Returns(tokenId);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        _repository.FindInvitations(blogId, invitedUserId, TokenType.BlogAssistantInvitation, default)
            .Returns(new List<Guid>());

        await _service.InviteAssistant(blogId, "invitee");

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.InviteAssistant, blog);
    }

    [Fact]
    public async Task ThrowWhenInvitingBlacklistedUser()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { invitedUserId } };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });

        var act = async () => await _service.InviteAssistant(blogId, "invitee");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("черного списка"));
    }

    [Fact]
    public async Task ThrowWhenInvitingBlockedUser()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        _userBlacklistChecker.IsBlockedAsync(userId, invitedUserId, default).Returns(true);

        var act = async () => await _service.InviteAssistant(blogId, "invitee");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.UnprocessableEntity && e.Message.Contains("личного черного списка"));
    }

    [Fact]
    public async Task PublishEventWhenCreatingInvitation()
    {
        var blogId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Title = "Test Blog", BlacklistedUserIds = new HashSet<Guid>() };
        var identity = AuthenticatedIdentities.Of(userId);

        _identityProvider.Current.Returns(identity);
        _guidFactory.Create().Returns(tokenId);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("invitee")
            .Returns(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        _repository.FindInvitations(blogId, invitedUserId, TokenType.BlogAssistantInvitation, default)
            .Returns(new List<Guid>());

        await _service.InviteAssistant(blogId, "invitee");

        await _eventProducer.Received(1).SendAsync(EventType.BlogInvitationCreated, tokenId);
    }

    [Fact]
    public async Task ThrowWhenAcceptingExpiredInvitation()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = AuthenticatedIdentities.Of(userId);
        var expiredTime = DateTimeOffset.UtcNow.AddDays(-1);
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            InvitedUser = new GeneralUser { UserId = userId },
            TargetRole = BlogRole.Assistant,
            ExpiresUtc = expiredTime
        };

        _identityProvider.Current.Returns(identity);
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        _repository.GetInvitation(tokenId, default).Returns(invitation);

        var act = async () => await _service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Gone && e.Message.Contains("истек"));
    }

    [Fact]
    public async Task ThrowWhenAcceptingInvitationForAnotherUser()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var identity = AuthenticatedIdentities.Of(userId);
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            InvitedUser = new GeneralUser { UserId = otherUserId },
            TargetRole = BlogRole.Assistant,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        _identityProvider.Current.Returns(identity);
        _repository.GetInvitation(tokenId, default).Returns(invitation);

        var act = async () => await _service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A reader invitation is redeemed past the public subscription door.
    /// </summary>
    /// <remarks>
    /// BlogService.Subscribe refuses a blog whose drafts are private, which is
    /// precisely the blog an invitation exists for: routed through it, the only
    /// path that redeems a reader invitation answered the invited user 403, and
    /// the invitation could never be accepted at all. The game side redeems its
    /// own reader invitation past its own public door for the same reason.
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

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(userId));
        _repository.GetInvitation(tokenId, default).Returns(invitation);

        await _service.AcceptInvitation(tokenId);

        await _subscriptionService.Received(1).SubscribeAsync(blogId, default);
        await _blogService.DidNotReceive().Subscribe(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeCancelInvitationAction()
    {
        var tokenId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = AuthenticatedIdentities.Of(userId);
        var blog = new BlogDto { Id = blogId };
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            BlogId = blogId,
            TargetRole = BlogRole.Assistant
        };

        _identityProvider.Current.Returns(identity);
        _repository.GetInvitation(tokenId, default).Returns(invitation);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);

        await _service.CancelInvitation(tokenId);

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CancelInvitation, blog);
    }

}
