using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Blog.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

public class BlogServiceShould : BlogServiceTestBase
{
    public BlogServiceShould()
    {
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// "Скрывать блоги" cleans what the viewer browses and leaves the moderation
    /// queue whole: a mentor who hides somebody's blogs still has to judge them.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task HideTheBlogsOfBlockedAuthorsOutsideTheModerationQueue(bool queue, bool hidden)
    {
        var viewerId = Guid.NewGuid();
        var blockedAuthorId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(viewerId, UserRole.Mentor));
        intentionManager.IsAllowed(BlogIntention.SetStatusModeration).Returns(true);
        userBlacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(viewerId, UserBlacklistSettings.HideBlogs, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { blockedAuthorId });
        var filter = queue
            ? new BlogFilter { PremoderationStatuses = [PremoderationStatus.AwaitingApproval] }
            : new BlogFilter();

        await service.GetPublicBlogs(new PagingQuery(), filter);

        await repository.Received().CountPublicBlogs(
            Arg.Is<BlogFilter>(f => hidden
                ? f.ExcludeOwnerIds != null && f.ExcludeOwnerIds.Contains(blockedAuthorId)
                : f.ExcludeOwnerIds == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeCreateBlogAction()
    {
        var blogId = Guid.NewGuid();
        var createBlog = new CreateBlog { Title = "Test Blog", DraftVisibility = DraftVisibility.Public };

        guidFactory.Create().Returns(blogId);
        repository.CreateBlog(Arg.Any<CreateBlogEntity>(), default).Returns(new BlogDto { Id = blogId });

        await service.Create(createBlog);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.Create);
    }

    [Fact]
    public async Task CreateBlogWithCorrectData()
    {
        var blogId = Guid.NewGuid();
        var createBlog = new CreateBlog
        {
            Title = "Test Blog",
            Description = "Description",
            DraftVisibility = DraftVisibility.Public
        };

        guidFactory.Create().Returns(blogId);
        repository.CreateBlog(Arg.Any<CreateBlogEntity>(), default).Returns(new BlogDto { Id = blogId });

        var result = await service.Create(createBlog);

        result.Id.Should().Be(blogId);
        await repository.Received(1).CreateBlog(
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

        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        repository.UpdateBlog(Arg.Any<UpdateBlogEntity>(), default).Returns(blog);

        await service.Update(updateBlog);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.EditSettings, blog);
    }

    /// <summary>
    /// A blog sent back to premoderation is hidden from its readers, so the list of
    /// the blogs they subscribed to leaves it out: named there, it would open as a
    /// blog that is not there.
    /// </summary>
    [Fact]
    public async Task LeaveABlogTheReaderMayNotOpenOutOfHisSubscribedBlogs()
    {
        var open = new BlogDto { Id = Guid.NewGuid(), Status = ModuleStatus.Active };
        var hidden = new BlogDto
        {
            Id = Guid.NewGuid(),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.AwaitingEdits
        };
        var ids = new[] { open.Id, hidden.Id };
        repository.GetByIds(ids, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new[] { open, hidden });

        var blogs = await service.GetSubscribedBlogs(ids);

        blogs.Select(b => b.Id).Should().Equal(open.Id);
    }

    [Fact]
    public async Task AuthorizeUpdateRubricAction()
    {
        var blogId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, DraftVisibility = DraftVisibility.Public };
        var updateRubric = new UpdateRubric { RubricId = rubricId, Title = "Renamed" };

        repository.GetRubric(rubricId, default).Returns((new Rubric { Id = rubricId }, blogId));
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        repository.UpdateRubric(Arg.Any<UpdateRubricEntity>(), default)
            .Returns(new Rubric { Id = rubricId, Title = "Renamed" });

        await service.UpdateRubric(updateRubric);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.ManageRubrics, blog);
        await repository.Received(1).UpdateRubric(
            Arg.Is<UpdateRubricEntity>(e => e.RubricId == rubricId && e.Title == "Renamed"), default);
    }

    [Fact]
    public async Task ThrowWhenUpdatingMissingRubric()
    {
        var rubricId = Guid.NewGuid();
        repository.GetRubric(rubricId, default).Returns(((Rubric?)null, Guid.Empty));

        var act = async () => await service.UpdateRubric(new UpdateRubric { RubricId = rubricId, Title = "X" });

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

        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        repository.ReorderRubrics(blogId, Arg.Any<IReadOnlyList<Guid>>(), default).Returns(Task.CompletedTask);
        // The order replaces the blog's whole order, so the body is checked against
        // the rubrics the blog holds before anything is written.
        repository.GetRubrics(blogId, default)
            .Returns(new[] { new Rubric { Id = first }, new Rubric { Id = second } });
        repository.GetRubricPublicationIds(blogId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, Guid[]>());

        await service.ReorderRubrics(blogId, orderedIds);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.ManageRubrics, blog);
        await repository.Received(1).ReorderRubrics(blogId, orderedIds, default);
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

        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        repository.GetRubrics(blogId, default)
            .Returns(new[] { new Rubric { Id = first }, new Rubric { Id = second } });

        var act = async () => await service.ReorderRubrics(blogId, new[] { second });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A refused order writes nothing.
        await repository.DidNotReceive().ReorderRubrics(
            blogId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FillRubricUnreadCountersForAuthenticatedViewer()
    {
        // (N/A): N = publications with unread content,
        // A = total unread comments across the rubric's publications.
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, "reader"));

        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();

        // The rubrics come embedded in the blog: the read of a blog is what the
        // page draws them from.
        var blog = new BlogDto
        {
            Id = blogId,
            DraftVisibility = DraftVisibility.Public,
            Rubrics =
            [
                new Rubric { Id = r1, PublicationCount = 2 },
                new Rubric { Id = r2, PublicationCount = 1 }
            ]
        };
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);

        // Blog-level unread fill runs inside GetAsync — return 0 for the blog.
        unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [blogId] = 0 });
        unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [blogId] = 0 });

        repository.GetRubricPublicationIds(blogId, Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, Guid[]>
        {
            [r1] = new[] { p1, p2 },
            [r2] = new[] { p3 }
        });
        unreadCountersRepository
            .SelectByEntitiesAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [p1] = 3, [p2] = 0, [p3] = 5 });

        var rubrics = (await service.GetAsync(blogId)).Rubrics.ToArray();

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
        var r1 = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var blog = new BlogDto
        {
            Id = blogId,
            DraftVisibility = DraftVisibility.Public,
            Rubrics = [new Rubric { Id = r1, PublicationCount = 2 }]
        };
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);

        repository.GetRubricPublicationIds(blogId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, Guid[]> { [r1] = new[] { p1, p2 } });
        unreadCountersRepository
            .SelectByEntitiesAsync(Guid.Empty, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { [p1] = 4, [p2] = 2 });

        var rubric = (await service.GetAsync(blogId)).Rubrics.Single();

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
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new BlogDto
        {
            Id = blogId,
            DraftVisibility = draftVisibility,
            PremoderationStatus = premoderationStatus
        });
        intentionManager
            .IsAllowed(BlogIntention.ViewDraft, Arg.Any<BlogDto>()).Returns(mayViewDraft);
        intentionManager
            .IsAllowed(BlogIntention.ViewPremoderationPending, Arg.Any<BlogDto>()).Returns(mayViewPending);

        var visible = await service.IsVisibleToViewerAsync(blogId);

        visible.Should().Be(expected);
    }

    [Fact]
    public async Task AnswerThatAnAbsentBlogIsNotVisible()
    {
        var blogId = Guid.NewGuid();
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);
        intentionManager
            .IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(true);

        var visible = await service.IsVisibleToViewerAsync(blogId);

        visible.Should().BeFalse();
    }

    /// <summary>
    /// The three single-blog reads answer on a blog the caller may not open
    /// exactly as on one that is not there.
    /// </summary>
    /// <remarks>
    /// A 403 says the identifier resolves, and the alias is five letters on routes open to guests.
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
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);
        repository.GetByPublicId("alias", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);
        repository.GetByOwnerUsernameAsync("owner", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);

        await ShouldBeNotFound(() => service.GetAsync(blogId));
        await ShouldBeNotFound(() => service.GetByPublicIdAsync("alias"));
        await ShouldBeNotFound(() => service.GetByOwnerUsernameAsync("owner"));
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
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(visible);
        intentionManager
            .When(m => m.ThrowIfForbidden(BlogIntention.EditSettings, visible))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "Недостаточно прав"));

        var act = async () => await service.Update(new UpdateBlog { BlogId = blogId, Title = "Правка" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    private static async Task ShouldBeNotFound(Func<Task> act) =>
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
}
