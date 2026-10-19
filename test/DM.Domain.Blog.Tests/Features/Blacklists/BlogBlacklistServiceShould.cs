using System;
using System.Collections.Generic;
using System.Linq;
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
using DM.Domain.Core.Users;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blacklists;

public class BlogBlacklistServiceShould : UnitTestBase
{
    private readonly IBlogBlacklistRepository repository;
    private readonly IBlogService blogService;
    private readonly IUserLookupService userLookupService;
    private readonly IIdentityProvider identityProvider;
    private readonly IIntentionManager intentionManager;
    private readonly IEventProducer producer;
    private readonly BlogBlacklistService service;

    public BlogBlacklistServiceShould()
    {
        repository = Mock<IBlogBlacklistRepository>();
        blogService = Mock<IBlogService>();
        userLookupService = Mock<IUserLookupService>();
        identityProvider = Mock<IIdentityProvider>();
        intentionManager = Mock<IIntentionManager>();
        producer = Mock<IEventProducer>();

        identityProvider.Current.Returns(Identity.Guest());

        service = new BlogBlacklistService(
            repository,
            blogService,
            userLookupService,
            identityProvider,
            intentionManager,
            producer);
    }

    private static BlogDto OpenBlog(Guid blogId, Guid? authorId = null, Guid? mentorId = null, params Guid[] blacklisted) => new()
    {
        Id = blogId,
        Author = new GeneralUser { UserId = authorId ?? Guid.NewGuid() },
        Mentor = mentorId is { } id ? new GeneralUser { UserId = id } : null,
        Assistants = new List<BlogAssistantInfo>(),
        BlacklistedUserIds = blacklisted.ToHashSet()
    };

    private BlogDto ArrangeSuccessfulAdd(Guid blogId, Guid userId, params Guid[] cancelledTokens)
    {
        var blog = OpenBlog(blogId);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        userLookupService.GetAsync("testuser").Returns(new GeneralUser { UserId = userId, Username = "testuser" });
        repository.CancelInvitationsForUser(blogId, userId, default).Returns(cancelledTokens);
        return blog;
    }

    /// <summary>
    /// A subscriber keeps the subscription: the service holds no subscription store to end it with.
    /// </summary>
    [Fact]
    public async Task AddTheUserToTheBlacklist()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        ArrangeSuccessfulAdd(blogId, userId);

        await service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        await repository.Received(1).Add(blogId, userId, Arg.Any<Guid>(), default);
    }

    [Fact]
    public async Task AnswerConflictForSomebodyAlreadyOnTheList()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        blogService.GetUnfiltered(blogId, default).Returns(OpenBlog(blogId, blacklisted: userId));
        userLookupService.GetAsync("testuser").Returns(new GeneralUser { UserId = userId, Username = "testuser" });

        var act = async () => await service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict && e.Message == RefusalMessage.AlreadyInBlacklist);
    }

    /// <summary>
    /// The request that lost the race is answered the way the check answers it.
    /// </summary>
    /// <remarks>
    /// Two requests for one name both pass the read, and the unique index refuses the second insert.
    /// </remarks>
    [Fact]
    public async Task AnswerConflictWhenTheInsertLosesTheRace()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        blogService.GetUnfiltered(blogId, default).Returns(OpenBlog(blogId));
        userLookupService.GetAsync("testuser").Returns(new GeneralUser { UserId = userId, Username = "testuser" });
        repository.Add(blogId, userId, Arg.Any<Guid>(), default)
            .Returns(Task.FromException(new DuplicateEntityException("Duplicate blog blacklist entry")));

        var act = async () => await service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict && e.Message == RefusalMessage.AlreadyInBlacklist);
        await repository.DidNotReceive().CancelInvitationsForUser(blogId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswerConflictForSomebodyNotOnTheList()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, BlacklistedUserIds = new HashSet<Guid>() };
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        userLookupService.GetAsync("testuser").Returns(new GeneralUser { UserId = userId, Username = "testuser" });

        var act = async () => await service.Remove(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict && e.Message == RefusalMessage.NotInBlacklist);
    }

    [Fact]
    public async Task AuthorizeGetBlacklistAction()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId };
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetBlacklist(blogId, default).Returns(new List<GeneralUser>());

        await service.Get(blogId);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.Edit, blog);
    }

    [Fact]
    public async Task AuthorizeAddToBlacklistAction()
    {
        var blogId = Guid.NewGuid();
        var blog = ArrangeSuccessfulAdd(blogId, Guid.NewGuid());

        await service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.Edit, blog);
    }

    [Fact]
    public async Task ThrowWhenBlacklistingBlogOwner()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        blogService.GetUnfiltered(blogId, default).Returns(OpenBlog(blogId, authorId: ownerId));
        userLookupService.GetAsync("owner").Returns(new GeneralUser { UserId = ownerId, Username = "owner" });

        var act = async () => await service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "owner" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.CannotBlacklistBlogLead);
    }

    [Fact]
    public async Task ThrowWhenBlacklistingBlogMentor()
    {
        var blogId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        blogService.GetUnfiltered(blogId, default).Returns(OpenBlog(blogId, mentorId: mentorId));
        userLookupService.GetAsync("mentor").Returns(new GeneralUser { UserId = mentorId, Username = "mentor" });

        var act = async () => await service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "mentor" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.CannotBlacklistBlogLead);
    }

    [Fact]
    public async Task PublishCancelledInvitationEventsWhenBlacklisting()
    {
        var blogId = Guid.NewGuid();
        var tokenId1 = Guid.NewGuid();
        var tokenId2 = Guid.NewGuid();
        ArrangeSuccessfulAdd(blogId, Guid.NewGuid(), tokenId1, tokenId2);

        await service.Add(new OperateBlogBlacklistLink { BlogId = blogId, Username = "testuser" });

        await producer.Received(1).SendAsync(EventType.BlogInvitationCancelled, tokenId1);
        await producer.Received(1).SendAsync(EventType.BlogInvitationCancelled, tokenId2);
    }
}
