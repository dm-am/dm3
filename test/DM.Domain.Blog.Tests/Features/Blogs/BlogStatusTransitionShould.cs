using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Statuses;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Blog.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

/// <summary>
/// The blog side of the status moves, one case a move: the table itself is ModuleStatusPolicyShould.
/// </summary>
public class BlogStatusTransitionShould : BlogServiceTestBase
{
    private readonly DateTimeOffset now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private UpdateBlogEntity? capturedUpdate;

    public BlogStatusTransitionShould()
    {
        // Every intention says yes by default, a test that means a stranger calls HideEveryBlogFromTheCaller.
        intentionManager
            .IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(true);
        guidFactory.Create().Returns(Guid.NewGuid());
        dateTimeProvider.Now.Returns(now);
    }

    private Guid SetupBlog(
        ModuleStatus status,
        ClosedReason closedReason = ClosedReason.None,
        DateTimeOffset? activatedUtc = null,
        DateTimeOffset? closedUtc = null,
        DraftVisibility draftVisibility = DraftVisibility.Public,
        PremoderationStatus premoderationStatus = PremoderationStatus.Approved)
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Status = status,
            ClosedReason = closedReason,
            ActivatedUtc = activatedUtc,
            ClosedUtc = closedUtc,
            DraftVisibility = draftVisibility,
            PremoderationStatus = premoderationStatus,
            Author = new GeneralUser { UserId = Guid.NewGuid() }
        };
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        repository.UpdateBlog(Arg.Do<UpdateBlogEntity>(update => capturedUpdate = update), Arg.Any<CancellationToken>())
            .Returns(blog);
        return blogId;
    }

    /// <summary>
    /// The caller passes one of the two view gates and no other intention.
    /// </summary>
    private void OpenOnly(BlogIntention gate)
    {
        HideEveryBlogFromTheCaller();
        intentionManager
            .IsAllowed(gate, Arg.Any<BlogDto>()).Returns(true);
    }

    private async Task<HttpException> RefusedStart(string id)
    {
        var act = async () => await service.ChangeStatusAsync(id, ModuleStatusTransition.Start);
        return (await act.Should().ThrowAsync<HttpException>()).Which;
    }

    [Fact]
    public async Task StartDraftBlogAndSetActivatedUtcOnFirstActivation()
    {
        var blogId = SetupBlog(ModuleStatus.Draft);

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        capturedUpdate.Should().NotBeNull();
        capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        capturedUpdate.ActivatedUtc.Should().Be(now);
        await eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogActive)), blogId);
    }

    [Fact]
    public async Task RejectStartOfAnActiveBlog()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        var act = async () => await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FreezeActiveBlogWithFrozenReason()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Freeze);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.Frozen);
        capturedUpdate.ClosedUtc.Should().Be(now);
        await eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogFrozen)), blogId);
    }

    [Fact]
    public async Task FinishActiveBlogWithFinishedReason()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Finish);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.Finished);
        capturedUpdate.ClosedUtc.Should().Be(now);
        await eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogFinished)), blogId);
    }

    [Fact]
    public async Task CloseActiveBlogWithNoneReasonAndSetClosedUtc()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Close);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        capturedUpdate.ClosedUtc.Should().Be(now);
        await eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogClosed)), blogId);
    }

    [Fact]
    public async Task ReopenAFrozenBlog()
    {
        var blogId = SetupBlog(ModuleStatus.Closed, ClosedReason.Frozen,
            activatedUtc: now.AddMonths(-2), closedUtc: now.AddDays(-7));

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Reopen);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        capturedUpdate.ClearClosedUtc.Should().BeTrue();
        capturedUpdate.ActivatedUtc.Should().BeNull(); // Already activated before
        await eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogActive)), blogId);
    }

    [Fact]
    public async Task ResolveBlogByPublicIdBeforeApplyingTransition()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Status = ModuleStatus.Draft };
        repository.GetByPublicId("abcde", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        repository.UpdateBlog(Arg.Do<UpdateBlogEntity>(update => capturedUpdate = update), Arg.Any<CancellationToken>())
            .Returns(blog);

        await service.ChangeStatusAsync("abcde", ModuleStatusTransition.Start);

        capturedUpdate!.BlogId.Should().Be(blogId);
        capturedUpdate.Status.Should().Be(ModuleStatus.Active);
    }

    [Fact]
    public async Task RejectStatusChangeOfMissingBlogWithNotFound()
    {
        var blogId = Guid.NewGuid();
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

        var act = async () => await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A blog the caller cannot find anywhere on the site does not exist for them
    /// here either: the same status and the same message an unclaimed id gets,
    /// whichever of the two gates is the one that closed.
    /// </summary>
    /// <remarks>
    /// Read without a scope, the codes 404, 400 and 403 would tell a stranger which hidden blogs exist.
    /// </remarks>
    [Theory]
    [InlineData(DraftVisibility.Private, PremoderationStatus.Approved)]
    [InlineData(DraftVisibility.Public, PremoderationStatus.AwaitingApproval)]
    public async Task AnswerOnAHiddenBlogTheWayItAnswersOnNoBlogAtAll(
        DraftVisibility visibility, PremoderationStatus premoderationStatus)
    {
        HideEveryBlogFromTheCaller();
        var hidden = SetupBlog(ModuleStatus.Draft,
            draftVisibility: visibility, premoderationStatus: premoderationStatus);
        var missing = Guid.NewGuid();
        repository.Get(missing, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

        var onHidden = await RefusedStart(hidden.ToString());
        var onMissing = await RefusedStart(missing.ToString());

        onHidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        onHidden.StatusCode.Should().Be(onMissing.StatusCode);
        onHidden.Message.Should().Be(onMissing.Message);
    }

    /// <summary>
    /// And the same through the public id, which is the surface a stranger can
    /// actually probe: five letters are guessable, a GUID is not.
    /// </summary>
    [Fact]
    public async Task AnswerOnAHiddenAliasTheWayItAnswersOnAnUnclaimedOne()
    {
        HideEveryBlogFromTheCaller();
        var hidden = new BlogDto
        {
            Id = Guid.NewGuid(),
            Status = ModuleStatus.Draft,
            DraftVisibility = DraftVisibility.Private,
            Author = new GeneralUser { UserId = Guid.NewGuid() }
        };
        repository.GetByPublicId("abcde", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);
        repository.GetByPublicId("fghij", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

        var onTaken = await RefusedStart("abcde");
        var onUnclaimed = await RefusedStart("fghij");

        onTaken.StatusCode.Should().Be(HttpStatusCode.NotFound);
        onTaken.StatusCode.Should().Be(onUnclaimed.StatusCode);
        onTaken.Message.Should().Be(onUnclaimed.Message);
    }

    /// <summary>
    /// The refusal comes before anything else looks at the blog, so neither the
    /// state machine nor the authorization gate can leak what the 404 withheld.
    /// </summary>
    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Active)]
    public async Task RefuseAHiddenBlogBeforeTheStateMachineSeesIt(ModuleStatus status)
    {
        HideEveryBlogFromTheCaller();
        // Premoderation hides a blog whatever its status, so it can hide an active
        // one; a private draft is hidden only while it is a draft.
        var blogId = SetupBlog(status, draftVisibility: DraftVisibility.Private,
            premoderationStatus: PremoderationStatus.AwaitingEdits);

        var refusal = await RefusedStart(blogId.ToString());

        refusal.StatusCode.Should().Be(HttpStatusCode.NotFound);
        intentionManager.DidNotReceive().ThrowIfForbidden(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>());
        await repository.DidNotReceive().UpdateBlog(Arg.Any<UpdateBlogEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The scope is the reader's ordinary one and nothing invented for this
    /// endpoint: the same two gates a plain read applies, the private draft and
    /// the pending verdict.
    /// </summary>
    [Fact]
    public async Task ReadTheBlogUnderTheTwoGatesAnOrdinaryReadApplies()
    {
        var blogId = SetupBlog(ModuleStatus.Draft,
            draftVisibility: DraftVisibility.Private,
            premoderationStatus: PremoderationStatus.AwaitingApproval);

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        intentionManager.Received(1).IsAllowed(BlogIntention.ViewDraft, Arg.Any<BlogDto>());
        intentionManager.Received(1).IsAllowed(BlogIntention.ViewPremoderationPending, Arg.Any<BlogDto>());
    }

    /// <summary>
    /// The owner of a private draft nobody else can open still moves it: the gate
    /// that closes for a stranger is exactly the one that opens for them.
    /// </summary>
    [Fact]
    public async Task LetTheOwnerStartAPrivateDraftNobodyElseCanSee()
    {
        OpenOnly(BlogIntention.ViewDraft);
        var blogId = SetupBlog(ModuleStatus.Draft, draftVisibility: DraftVisibility.Private);

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
    }

    /// <summary>
    /// And whoever may see a blog awaiting a verdict - its leads, its mentor,
    /// whoever may pass the verdict - reaches the machine and the authorization
    /// gate, which is where a 403 still belongs.
    /// </summary>
    [Fact]
    public async Task LetWhoeverMaySeeAPremoderatedBlogReachTheStatusMachine()
    {
        OpenOnly(BlogIntention.ViewPremoderationPending);
        var blogId = SetupBlog(ModuleStatus.Draft,
            premoderationStatus: PremoderationStatus.AwaitingApproval);

        await service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.SetStatusActive, Arg.Any<BlogDto>());
    }
}
