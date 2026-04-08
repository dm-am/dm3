using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
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
using DM.Domain.Account.Features.Authentication;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Invitations;

public class BlogInvitationServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IBlogInvitationRepository> _repository;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IBlogService> _blogService;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IUserBlacklistChecker> _userBlacklistChecker;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly BlogInvitationService _service;

    public BlogInvitationServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _repository = Mock<IBlogInvitationRepository>();
        _intentionManager = Mock<IIntentionManager>();
        _blogService = Mock<IBlogService>();
        _userLookupService = Mock<IUserLookupService>();
        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _eventProducer = Mock<IEventProducer>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);
        _userBlacklistChecker.Setup(c => c.IsBlockedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync(false);

        _service = new BlogInvitationService(
            _identityProvider.Object,
            _guidFactory.Object,
            _repository.Object,
            _intentionManager.Object,
            _blogService.Object,
            _userLookupService.Object,
            _userBlacklistChecker.Object,
            _eventProducer.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task AuthorizeInviteAssistantAction()
    {
        var blogId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Title = "Test Blog", BlacklistedUserIds = new HashSet<Guid>() };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _guidFactory.Setup(f => f.Create()).Returns(tokenId);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("invitee"))
            .ReturnsAsync(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        _repository.Setup(r => r.FindInvitations(blogId, invitedUserId, TokenType.BlogAssistantInvitation, default))
            .ReturnsAsync(new List<Guid>());

        await _service.InviteAssistant(blogId, "invitee");

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.InviteAssistant, blog), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenInvitingBlacklistedUser()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid> { invitedUserId } };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("invitee"))
            .ReturnsAsync(new GeneralUser { UserId = invitedUserId, Username = "invitee" });

        var act = async () => await _service.InviteAssistant(blogId, "invitee");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("blacklisted"));
    }

    [Fact]
    public async Task ThrowWhenInvitingBlockedUser()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("invitee"))
            .ReturnsAsync(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        _userBlacklistChecker.Setup(c => c.IsBlockedAsync(userId, invitedUserId, default)).ReturnsAsync(true);

        var act = async () => await _service.InviteAssistant(blogId, "invitee");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.UnprocessableEntity && e.Message.Contains("blocked"));
    }

    [Fact]
    public async Task PublishEventWhenCreatingInvitation()
    {
        var blogId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Title = "Test Blog", BlacklistedUserIds = new HashSet<Guid>() };
        var identity = CreateAuthenticatedIdentity(userId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _guidFactory.Setup(f => f.Create()).Returns(tokenId);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("invitee"))
            .ReturnsAsync(new GeneralUser { UserId = invitedUserId, Username = "invitee" });
        _repository.Setup(r => r.FindInvitations(blogId, invitedUserId, TokenType.BlogAssistantInvitation, default))
            .ReturnsAsync(new List<Guid>());

        await _service.InviteAssistant(blogId, "invitee");

        _eventProducer.Verify(p => p.SendAsync(EventType.BlogInvitationCreated, tokenId), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenAcceptingExpiredInvitation()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = CreateAuthenticatedIdentity(userId);
        var expiredTime = DateTimeOffset.UtcNow.AddDays(-1);
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            InvitedUser = new GeneralUser { UserId = userId },
            TargetRole = BlogRole.Assistant,
            ExpiresUtc = expiredTime
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);
        _repository.Setup(r => r.GetInvitation(tokenId, default)).ReturnsAsync(invitation);

        var act = async () => await _service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Gone && e.Message.Contains("expired"));
    }

    [Fact]
    public async Task ThrowWhenAcceptingInvitationForAnotherUser()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var identity = CreateAuthenticatedIdentity(userId);
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            InvitedUser = new GeneralUser { UserId = otherUserId },
            TargetRole = BlogRole.Assistant,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetInvitation(tokenId, default)).ReturnsAsync(invitation);

        var act = async () => await _service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeCancelInvitationAction()
    {
        var tokenId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var identity = CreateAuthenticatedIdentity(userId);
        var blog = new BlogDto { Id = blogId };
        var invitation = new BlogInvitation
        {
            TokenId = tokenId,
            BlogId = blogId,
            TargetRole = BlogRole.Assistant
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetInvitation(tokenId, default)).ReturnsAsync(invitation);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);

        await _service.CancelInvitation(tokenId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.CancelInvitation, blog), Times.Once);
    }

    private static IIdentity CreateAuthenticatedIdentity(Guid userId)
    {
        var user = new AuthenticatedUser { UserId = userId, Username = "testuser" };
        var session = new Session();
        return Identity.Success(user, session, UserSettings.Default, "token");
    }
}
