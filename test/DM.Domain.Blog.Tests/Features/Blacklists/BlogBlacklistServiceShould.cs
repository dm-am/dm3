using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blacklists;

public class BlogBlacklistServiceShould : UnitTestBase
{
    private readonly IBlogBlacklistRepository _repository;
    private readonly IBlogService _blogService;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IEventProducer _producer;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly BlogBlacklistService _service;

    public BlogBlacklistServiceShould()
    {
        _repository = Mock<IBlogBlacklistRepository>();
        _blogService = Mock<IBlogService>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();
        _producer = Mock<IEventProducer>();
        _subscriptionRepository = Mock<ISubscriptionRepository>();

        _identityProvider.Current.Returns(Identity.Guest());

        _service = new BlogBlacklistService(
            _repository,
            _blogService,
            _userLookupService,
            _identityProvider,
            _intentionManager,
            _producer,
            _subscriptionRepository);
    }

    [Fact]
    public async Task DropTheBlogSubscriptionOfTheUserItBlacklists()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            Assistants = new List<BlogAssistantInfo>(),
            BlacklistedUserIds = new HashSet<Guid>()
        };
        var user = new GeneralUser { UserId = userId, Username = "testuser" };

        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("testuser").Returns(user);
        _repository.IsBlocked(blogId, userId, default).Returns(false);
        _repository.Add(blogId, userId, Arg.Any<Guid>(), default).Returns(Task.CompletedTask);
        _repository.CancelInvitationsForUser(blogId, userId, default).Returns(new List<Guid>());

        await _service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        // A blog has no command for removing a reader, so the entry is the one
        // that ends the subscription: left alone, the blacklisted user stayed in
        // the list of readers and on the fan-out of every publication.
        await _subscriptionRepository.Received(1).DeleteAsync(userId, SubscriptionTargetType.Blog, blogId, default);
    }

    [Fact]
    public async Task AuthorizeGetBlacklistAction()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId };
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetBlacklist(blogId, default).Returns(new List<GeneralUser>());

        await _service.Get(blogId);

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.Edit, blog);
    }

    [Fact]
    public async Task AuthorizeAddToBlacklistAction()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            Assistants = new List<BlogAssistantInfo>(),
            BlacklistedUserIds = new HashSet<Guid>()
        };
        var user = new GeneralUser { UserId = userId, Username = "testuser" };

        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("testuser").Returns(user);
        _repository.IsBlocked(blogId, userId, default).Returns(false);
        _repository.Add(blogId, userId, Arg.Any<Guid>(), default).Returns(Task.CompletedTask);
        _repository.CancelInvitationsForUser(blogId, userId, default).Returns(new List<Guid>());

        await _service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.Edit, blog);
    }

    [Fact]
    public async Task ThrowWhenBlacklistingBlogOwner()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = ownerId },
            Assistants = new List<BlogAssistantInfo>(),
            BlacklistedUserIds = new HashSet<Guid>()
        };
        var user = new GeneralUser { UserId = ownerId, Username = "owner" };

        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("owner").Returns(user);

        var act = async () => await _service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "owner" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("автора блога"));
    }

    [Fact]
    public async Task ThrowWhenBlacklistingBlogMentor()
    {
        var blogId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            Mentor = new GeneralUser { UserId = mentorId },
            Assistants = new List<BlogAssistantInfo>(),
            BlacklistedUserIds = new HashSet<Guid>()
        };
        var user = new GeneralUser { UserId = mentorId, Username = "mentor" };

        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("mentor").Returns(user);

        var act = async () => await _service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "mentor" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("наставника"));
    }

    [Fact]
    public async Task PublishCancelledInvitationEventsWhenBlacklisting()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tokenId1 = Guid.NewGuid();
        var tokenId2 = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            Assistants = new List<BlogAssistantInfo>(),
            BlacklistedUserIds = new HashSet<Guid>()
        };
        var user = new GeneralUser { UserId = userId, Username = "testuser" };

        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _userLookupService.GetAsync("testuser").Returns(user);
        _repository.IsBlocked(blogId, userId, default).Returns(false);
        _repository.Add(blogId, userId, Arg.Any<Guid>(), default).Returns(Task.CompletedTask);
        _repository.CancelInvitationsForUser(blogId, userId, default).Returns(new List<Guid> { tokenId1, tokenId2 });
        _producer.SendAsync(EventType.BlogInvitationCancelled, Arg.Any<Guid>()).Returns(Task.CompletedTask);

        await _service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        await _producer.Received(1).SendAsync(EventType.BlogInvitationCancelled, tokenId1);
        await _producer.Received(1).SendAsync(EventType.BlogInvitationCancelled, tokenId2);
    }
}
