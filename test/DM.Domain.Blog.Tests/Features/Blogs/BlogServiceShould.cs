using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class BlogServiceShould : UnitTestBase
{
    private readonly Mock<IBlogRepository> _repository;
    private readonly Mock<IBlogBlacklistRepository> _blacklistRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IBlogSubscriptionService> _subscriptionService;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IValidator<CreateBlog>> _createBlogValidator;
    private readonly Mock<IValidator<UpdateBlog>> _updateBlogValidator;
    private readonly Mock<IValidator<CreateRubric>> _createRubricValidator;
    private readonly Mock<IValidator<UpdateRubric>> _updateRubricValidator;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly BlogService _service;

    public BlogServiceShould()
    {
        _repository = Mock<IBlogRepository>();
        _blacklistRepository = Mock<IBlogBlacklistRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _subscriptionService = Mock<IBlogSubscriptionService>();
        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();
        _createBlogValidator = Mock<IValidator<CreateBlog>>();
        _updateBlogValidator = Mock<IValidator<UpdateBlog>>();
        _createRubricValidator = Mock<IValidator<CreateRubric>>();
        _updateRubricValidator = Mock<IValidator<UpdateRubric>>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _eventProducer = Mock<IEventProducer>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _createBlogValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateBlog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateBlogValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateBlog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateRubricValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateRubric>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _service = new BlogService(
            _repository.Object,
            _blacklistRepository.Object,
            _userLookupService.Object,
            _subscriptionService.Object,
            _unreadCountersRepository.Object,
            _identityProvider.Object,
            _intentionManager.Object,
            _createBlogValidator.Object,
            _updateBlogValidator.Object,
            _createRubricValidator.Object,
            _updateRubricValidator.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _eventProducer.Object);
    }

    [Fact]
    public async Task AuthorizeCreateBlogAction()
    {
        var blogId = Guid.NewGuid();
        var createBlog = new CreateBlog { Title = "Test Blog", DraftVisibility = DraftVisibility.Public };

        _guidFactory.Setup(f => f.Create()).Returns(blogId);
        _repository.Setup(r => r.CreateBlog(It.IsAny<CreateBlogEntity>(), default))
            .ReturnsAsync(new BlogDto { Id = blogId });

        await _service.Create(createBlog);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.Create), Times.Once);
    }

    [Fact]
    public async Task CreateBlogWithCorrectData()
    {
        var blogId = Guid.NewGuid();
        var createBlog = new CreateBlog
        {
            Title = "Test Blog",
            Description = "Description",
            DraftVisibility = DraftVisibility.Public,
            CommentsEnabled = true
        };

        _guidFactory.Setup(f => f.Create()).Returns(blogId);
        _repository.Setup(r => r.CreateBlog(It.IsAny<CreateBlogEntity>(), default))
            .ReturnsAsync(new BlogDto { Id = blogId });

        var result = await _service.Create(createBlog);

        result.Id.Should().Be(blogId);
        _repository.Verify(r => r.CreateBlog(
            It.Is<CreateBlogEntity>(e =>
                e.BlogId == blogId &&
                e.Title == "Test Blog" &&
                e.Description == "Description"),
            default), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateBlogAction()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        var updateBlog = new UpdateBlog { BlogId = blogId, Title = "Updated" };

        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), default))
            .ReturnsAsync(blog);

        await _service.Update(updateBlog);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.EditSettings, blog), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenSubscribingToOwnBlog()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new AuthenticatedUser { UserId = userId, Username = "user" };
        var session = new Session();
        var identity = Identity.Success(user, session, UserSettings.Default, "token");

        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = userId }
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(blog);

        var act = async () => await _service.Subscribe(blogId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("собственный блог"));
    }

    [Fact]
    public async Task AuthorizeUpdateRubricAction()
    {
        var blogId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        var updateRubric = new UpdateRubric { RubricId = rubricId, Title = "Renamed" };

        _repository.Setup(r => r.GetRubric(rubricId, default))
            .ReturnsAsync((new Rubric { Id = rubricId }, blogId));
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateRubric(It.IsAny<UpdateRubricEntity>(), default))
            .ReturnsAsync(new Rubric { Id = rubricId, Title = "Renamed" });

        await _service.UpdateRubric(updateRubric);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.CreateRubric, blog), Times.Once);
        _repository.Verify(r => r.UpdateRubric(
            It.Is<UpdateRubricEntity>(e => e.RubricId == rubricId && e.Title == "Renamed"), default), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenUpdatingMissingRubric()
    {
        var rubricId = Guid.NewGuid();
        _repository.Setup(r => r.GetRubric(rubricId, default))
            .ReturnsAsync(((Rubric?)null, Guid.Empty));

        var act = async () => await _service.UpdateRubric(new UpdateRubric { RubricId = rubricId, Title = "X" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeReorderRubricsAction()
    {
        var blogId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var orderedIds = new[] { second, first };
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };

        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(blog);
        _repository.Setup(r => r.ReorderRubrics(blogId, It.IsAny<IReadOnlyList<Guid>>(), default))
            .Returns(Task.CompletedTask);
        // The order replaces the blog's whole order, so the body is checked against
        // the rubrics the blog holds before anything is written.
        _repository.Setup(r => r.GetRubrics(blogId, default))
            .ReturnsAsync(new[] { new Rubric { Id = first }, new Rubric { Id = second } });
        _repository.Setup(r => r.GetRubricPublicationIds(blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid[]>());

        await _service.ReorderRubrics(blogId, orderedIds);

        _intentionManager.Verify(m => m.ThrowIfForbidden(BlogIntention.CreateRubric, blog), Times.Once);
        _repository.Verify(r => r.ReorderRubrics(blogId, orderedIds, default), Times.Once);
    }

    /// <summary>
    /// A list that skips a rubric is refused rather than half-applied: the rubric
    /// it left out would keep the sort order the same call has just given to
    /// another one, and two rubrics would share a position.
    /// </summary>
    [Fact]
    public async Task RefuseARubricOrderThatSkipsARubric()
    {
        var blogId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };

        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(blog);
        _repository.Setup(r => r.GetRubrics(blogId, default))
            .ReturnsAsync(new[] { new Rubric { Id = first }, new Rubric { Id = second } });

        var act = async () => await _service.ReorderRubrics(blogId, new[] { second });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _repository.Verify(
            r => r.ReorderRubrics(blogId, It.IsAny<IReadOnlyList<Guid>>(), default), Times.Never,
            "a refused order writes nothing");
    }

    [Fact]
    public async Task FillRubricUnreadCountersForAuthenticatedViewer()
    {
        // (N/A) per doc 4.2.1.4: N = publications with unread content,
        // A = total unread comments across the rubric's publications.
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new AuthenticatedUser
        {
            UserId = userId,
            Username = "reader",
            Role = UserRole.RegularUser
        };
        _identityProvider.Setup(p => p.Current)
            .Returns(Identity.Success(user, new Session(), UserSettings.Default, "token"));

        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(blog);

        // Blog-level unread fill runs inside GetAsync — return 0 for the blog.
        _unreadCountersRepository
            .Setup(r => r.SelectByParentsAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [blogId] = 0 });
        _unreadCountersRepository
            .Setup(r => r.SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [blogId] = 0 });

        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();

        _repository.Setup(r => r.GetRubrics(blogId, default)).ReturnsAsync(new[]
        {
            new Rubric { Id = r1, PublicationCount = 2 },
            new Rubric { Id = r2, PublicationCount = 1 }
        });
        _repository.Setup(r => r.GetRubricPublicationIds(blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid[]>
            {
                [r1] = new[] { p1, p2 },
                [r2] = new[] { p3 }
            });
        _unreadCountersRepository
            .Setup(r => r.SelectByEntitiesAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [p1] = 3, [p2] = 0, [p3] = 5 });

        var rubrics = (await _service.GetRubrics(blogId)).ToArray();

        var first = rubrics.Single(r => r.Id == r1);
        first.UnreadPublicationsCount.Should().Be(1); // only p1 has unread comments
        first.UnreadCommentsCount.Should().Be(3);     // 3 + 0

        var second = rubrics.Single(r => r.Id == r2);
        second.UnreadPublicationsCount.Should().Be(1);
        second.UnreadCommentsCount.Should().Be(5);
    }

    [Fact]
    public async Task FillRubricUnreadCountersForAnonymousViewer()
    {
        // Default identity is Guest — every publication counts as unread (N =
        // total publications) and A is the total comment count for the rubric.
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(blog);

        var r1 = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        _repository.Setup(r => r.GetRubrics(blogId, default)).ReturnsAsync(new[]
        {
            new Rubric { Id = r1, PublicationCount = 2 }
        });
        _repository.Setup(r => r.GetRubricPublicationIds(blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Guid[]> { [r1] = new[] { p1, p2 } });
        _unreadCountersRepository
            .Setup(r => r.SelectByEntitiesAsync(Guid.Empty, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [p1] = 4, [p2] = 2 });

        var rubric = (await _service.GetRubrics(blogId)).Single();

        rubric.UnreadPublicationsCount.Should().Be(2); // anon: N = total publications
        rubric.UnreadCommentsCount.Should().Be(6);     // 4 + 2 total comments
    }
}
