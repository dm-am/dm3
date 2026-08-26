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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class BlogServiceShould : UnitTestBase
{
    private readonly IBlogRepository _repository;
    private readonly IBlogBlacklistRepository _blacklistRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IBlogSubscriptionService _subscriptionService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IValidator<CreateBlog> _createBlogValidator;
    private readonly IValidator<UpdateBlog> _updateBlogValidator;
    private readonly IValidator<CreateRubric> _createRubricValidator;
    private readonly IValidator<UpdateRubric> _updateRubricValidator;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;
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

        _identityProvider.Current.Returns(Identity.Guest());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _createBlogValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateBlog>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _updateBlogValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateBlog>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _updateRubricValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateRubric>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _service = new BlogService(
            _repository,
            _blacklistRepository,
            _userLookupService,
            _subscriptionService,
            _unreadCountersRepository,
            _identityProvider,
            _intentionManager,
            _createBlogValidator,
            _updateBlogValidator,
            _createRubricValidator,
            _updateRubricValidator,
            _guidFactory,
            _dateTimeProvider,
            _eventProducer);
    }

    [Fact]
    public async Task AuthorizeCreateBlogAction()
    {
        var blogId = Guid.NewGuid();
        var createBlog = new CreateBlog { Title = "Test Blog", DraftVisibility = DraftVisibility.Public };

        _guidFactory.Create().Returns(blogId);
        _repository.CreateBlog(Arg.Any<CreateBlogEntity>(), default).Returns(new BlogDto { Id = blogId });

        await _service.Create(createBlog);

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.Create);
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

        _guidFactory.Create().Returns(blogId);
        _repository.CreateBlog(Arg.Any<CreateBlogEntity>(), default).Returns(new BlogDto { Id = blogId });

        var result = await _service.Create(createBlog);

        result.Id.Should().Be(blogId);
        await _repository.Received(1).CreateBlog(
            Arg.Is<CreateBlogEntity>(e =>
                e.BlogId == blogId &&
                e.Title == "Test Blog" &&
                e.Description == "Description"),
            default);
    }

    [Fact]
    public async Task AuthorizeUpdateBlogAction()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        var updateBlog = new UpdateBlog { BlogId = blogId, Title = "Updated" };

        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        _repository.UpdateBlog(Arg.Any<UpdateBlogEntity>(), default).Returns(blog);

        await _service.Update(updateBlog);

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.EditSettings, blog);
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

        _identityProvider.Current.Returns(identity);
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);

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

        _repository.GetRubric(rubricId, default).Returns((new Rubric { Id = rubricId }, blogId));
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        _repository.UpdateRubric(Arg.Any<UpdateRubricEntity>(), default)
            .Returns(new Rubric { Id = rubricId, Title = "Renamed" });

        await _service.UpdateRubric(updateRubric);

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CreateRubric, blog);
        await _repository.Received(1).UpdateRubric(
            Arg.Is<UpdateRubricEntity>(e => e.RubricId == rubricId && e.Title == "Renamed"), default);
    }

    [Fact]
    public async Task ThrowWhenUpdatingMissingRubric()
    {
        var rubricId = Guid.NewGuid();
        _repository.GetRubric(rubricId, default).Returns(((Rubric?)null, Guid.Empty));

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

        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        _repository.ReorderRubrics(blogId, Arg.Any<IReadOnlyList<Guid>>(), default).Returns(Task.CompletedTask);
        // The order replaces the blog's whole order, so the body is checked against
        // the rubrics the blog holds before anything is written.
        _repository.GetRubrics(blogId, default)
            .Returns(new[] { new Rubric { Id = first }, new Rubric { Id = second } });
        _repository.GetRubricPublicationIds(blogId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, Guid[]>());

        await _service.ReorderRubrics(blogId, orderedIds);

        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.CreateRubric, blog);
        await _repository.Received(1).ReorderRubrics(blogId, orderedIds, default);
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

        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        _repository.GetRubrics(blogId, default)
            .Returns(new[] { new Rubric { Id = first }, new Rubric { Id = second } });

        var act = async () => await _service.ReorderRubrics(blogId, new[] { second });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A refused order writes nothing.
        await _repository.DidNotReceive().ReorderRubrics(
            blogId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
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
        _identityProvider.Current.Returns(Identity.Success(user, new Session(), UserSettings.Default, "token"));

        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);

        // Blog-level unread fill runs inside GetAsync — return 0 for the blog.
        _unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [blogId] = 0 });
        _unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [blogId] = 0 });

        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();

        _repository.GetRubrics(blogId, default).Returns(new[]
        {
            new Rubric { Id = r1, PublicationCount = 2 },
            new Rubric { Id = r2, PublicationCount = 1 }
        });
        _repository.GetRubricPublicationIds(blogId, Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, Guid[]>
        {
            [r1] = new[] { p1, p2 },
            [r2] = new[] { p3 }
        });
        _unreadCountersRepository
            .SelectByEntitiesAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [p1] = 3, [p2] = 0, [p3] = 5 });

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
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);

        var r1 = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        _repository.GetRubrics(blogId, default).Returns(new[]
        {
            new Rubric { Id = r1, PublicationCount = 2 }
        });
        _repository.GetRubricPublicationIds(blogId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, Guid[]> { [r1] = new[] { p1, p2 } });
        _unreadCountersRepository
            .SelectByEntitiesAsync(Guid.Empty, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [p1] = 4, [p2] = 2 });

        var rubric = (await _service.GetRubrics(blogId)).Single();

        rubric.UnreadPublicationsCount.Should().Be(2); // anon: N = total publications
        rubric.UnreadCommentsCount.Should().Be(6);     // 4 + 2 total comments
    }

    /// <summary>
    /// The two gates GetAsync throws on, asked instead of thrown: the form
    /// everything living inside a blog needs, because their refusal has to be a
    /// 404 and a thrown gate produces a 403.
    /// </summary>
    /// <remarks>
    /// A blog that is not there answers the same as one the caller may not open:
    /// the whole point of the question is that the two cannot be told apart from
    /// outside.
    /// </remarks>
    [Theory]
    [InlineData(DraftVisibility.Public, PremoderationStatus.Approved, true, true, true)]
    [InlineData(DraftVisibility.Private, PremoderationStatus.Approved, false, true, false)]
    [InlineData(DraftVisibility.Private, PremoderationStatus.Approved, true, true, true)]
    [InlineData(DraftVisibility.Public, PremoderationStatus.AwaitingApproval, true, false, false)]
    [InlineData(DraftVisibility.Public, PremoderationStatus.AwaitingEdits, true, false, false)]
    [InlineData(DraftVisibility.Public, PremoderationStatus.AwaitingApproval, true, true, true)]
    public async Task AnswerWhetherTheViewerMaySeeTheBlog(
        DraftVisibility draftVisibility,
        PremoderationStatus premoderationStatus,
        bool mayViewDraft,
        bool mayViewPending,
        bool expected)
    {
        var blogId = Guid.NewGuid();
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new BlogDto
        {
            Id = blogId,
            DraftVisibility = draftVisibility,
            PremoderationStatus = premoderationStatus
        });
        _intentionManager
            .IsAllowed(BlogIntention.ViewDraft, Arg.Any<BlogDto>()).Returns(mayViewDraft);
        _intentionManager
            .IsAllowed(BlogIntention.ViewPremoderationPending, Arg.Any<BlogDto>()).Returns(mayViewPending);

        var visible = await _service.IsVisibleToViewerAsync(blogId);

        visible.Should().Be(expected);
    }

    [Fact]
    public async Task AnswerThatAnAbsentBlogIsNotVisible()
    {
        var blogId = Guid.NewGuid();
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);
        _intentionManager
            .IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(true);

        var visible = await _service.IsVisibleToViewerAsync(blogId);

        visible.Should().BeFalse();
    }

    /// <summary>
    /// The three single-blog reads answer on a blog the caller may not open
    /// exactly as on one that is not there.
    /// </summary>
    /// <remarks>
    /// They used to throw the visibility gate instead of asking it, which made
    /// every one of them a 403 — and a 403 says the identifier resolves. The
    /// alias is five letters and the routes are open to guests, so that was the
    /// same oracle the two status endpoints were closed against, reached by an
    /// ordinary read.
    /// </remarks>
    [Theory]
    [InlineData(DraftVisibility.Private, PremoderationStatus.Approved)]
    [InlineData(DraftVisibility.Public, PremoderationStatus.AwaitingApproval)]
    [InlineData(DraftVisibility.Public, PremoderationStatus.AwaitingEdits)]
    public async Task AnswerOnAHiddenBlogTheWayItAnswersOnNoBlogAtAll(
        DraftVisibility draftVisibility, PremoderationStatus premoderationStatus)
    {
        var blogId = Guid.NewGuid();
        var hidden = new BlogDto
        {
            Id = blogId,
            PublicId = "alias",
            DraftVisibility = draftVisibility,
            PremoderationStatus = premoderationStatus,
            Author = new GeneralUser { Username = "owner" }
        };
        HideEveryBlogFromTheCaller();
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);
        _repository.GetByPublicId("alias", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);
        _repository.GetByOwnerUsernameAsync("owner", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);

        await ShouldBeNotFound(() => _service.GetAsync(blogId));
        await ShouldBeNotFound(() => _service.GetByPublicIdAsync("alias"));
        await ShouldBeNotFound(() => _service.GetByOwnerUsernameAsync("owner"));
    }

    /// <summary>
    /// The other side of the line: a blog the caller can see is read, and the
    /// refusal of an action on it stays the 403 the action's own gate produces.
    /// </summary>
    [Fact]
    public async Task StillRefuseAnActionOnAVisibleBlogWithForbidden()
    {
        var blogId = Guid.NewGuid();
        var visible = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(visible);
        _intentionManager
            .When(m => m.ThrowIfForbidden(BlogIntention.EditSettings, visible))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав"));

        var act = async () => await _service.Update(new UpdateBlog { BlogId = blogId, Title = "Правка" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    private void HideEveryBlogFromTheCaller() =>
        _intentionManager
            .IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(false);

    private static async Task ShouldBeNotFound(Func<Task> act) =>
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
}
