using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
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
using DM.Domain.Core.Users;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blacklists;

public class BlogBlacklistServiceShould : UnitTestBase
{
    private readonly Mock<IBlogBlacklistRepository> _repository;
    private readonly Mock<IBlogService> _blogService;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IEventProducer> _producer;
    private readonly Mock<IMapper> _mapper;
    private readonly BlogBlacklistService _service;

    public BlogBlacklistServiceShould()
    {
        _repository = Mock<IBlogBlacklistRepository>();
        _blogService = Mock<IBlogService>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();
        _producer = Mock<IEventProducer>();
        _mapper = Mock<IMapper>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        _service = new BlogBlacklistService(
            _repository.Object,
            _blogService.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _intentionManager.Object,
            _producer.Object,
            _mapper.Object);
    }

    [Fact]
    public async Task AuthorizeGetBlacklistAction()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId };
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.GetBlacklist(blogId, default)).ReturnsAsync(new List<GeneralUser>());

        await _service.GetBlacklistAsync(blogId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.Edit, blog), Times.Once);
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

        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("testuser")).ReturnsAsync(user);
        _repository.Setup(r => r.IsBlocked(blogId, userId, default)).ReturnsAsync(false);
        _repository.Setup(r => r.Add(blogId, userId, It.IsAny<Guid>(), default)).Returns(Task.CompletedTask);
        _repository.Setup(r => r.CancelInvitationsForUser(blogId, userId, default))
            .ReturnsAsync(new List<Guid>());
        _mapper.Setup(m => m.Map<GeneralUser>(user)).Returns(user);

        await _service.AddToBlacklistAsync(blogId, "testuser");

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.Edit, blog), Times.Once);
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

        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("owner")).ReturnsAsync(user);

        var act = async () => await _service.AddToBlacklistAsync(blogId, "owner");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("owner"));
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

        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("mentor")).ReturnsAsync(user);

        var act = async () => await _service.AddToBlacklistAsync(blogId, "mentor");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("mentor"));
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

        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _userLookupService.Setup(s => s.GetAsync("testuser")).ReturnsAsync(user);
        _repository.Setup(r => r.IsBlocked(blogId, userId, default)).ReturnsAsync(false);
        _repository.Setup(r => r.Add(blogId, userId, It.IsAny<Guid>(), default)).Returns(Task.CompletedTask);
        _repository.Setup(r => r.CancelInvitationsForUser(blogId, userId, default))
            .ReturnsAsync(new List<Guid> { tokenId1, tokenId2 });
        _mapper.Setup(m => m.Map<GeneralUser>(user)).Returns(user);
        _producer.Setup(p => p.SendAsync(EventType.BlogInvitationCancelled, It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        await _service.AddToBlacklistAsync(blogId, "testuser");

        _producer.Verify(p => p.SendAsync(EventType.BlogInvitationCancelled, tokenId1), Times.Once);
        _producer.Verify(p => p.SendAsync(EventType.BlogInvitationCancelled, tokenId2), Times.Once);
    }
}
