using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Statuses;
using DM.Domain.Account.Features.Authentication;
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
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

/// <summary>
/// Blog status state machine tests. Mirror GameStatusTransitionShould
/// one-to-one: the same transitions, the same legality rules, the same
/// ActivatedUtc / ClosedUtc / ClosedReason handling.
/// </summary>
public class BlogStatusTransitionShould : UnitTestBase
{
    private readonly Mock<IBlogRepository> _repository;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly BlogService _service;
    private readonly DateTimeOffset _now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private UpdateBlogEntity? _capturedUpdate;

    public BlogStatusTransitionShould()
    {
        _repository = Mock<IBlogRepository>();
        var blacklistRepository = Mock<IBlogBlacklistRepository>();
        var userLookupService = Mock<IUserLookupService>();
        var subscriptionService = Mock<IBlogSubscriptionService>();
        var unreadCountersRepository = Mock<IUnreadCountersRepository>();

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<BlogIntention>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()));
        // Every intention says yes by default, which is the same answer the no-op
        // ThrowIfForbidden above gives. The visibility gates ask instead of
        // throwing, so without this the whole machine would be exercised as a
        // stranger and answer 404 everywhere. The tests that mean to be a stranger
        // call HideEveryBlogFromTheCaller.
        _intentionManager
            .Setup(m => m.IsAllowed(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()))
            .Returns(true);

        var createBlogValidator = Mock<IValidator<CreateBlog>>();
        var updateBlogValidator = Mock<IValidator<UpdateBlog>>();
        var createRubricValidator = Mock<IValidator<CreateRubric>>();
        var updateRubricValidator = Mock<IValidator<UpdateRubric>>();

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(_now);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<IEnumerable<EventType>>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _service = new BlogService(
            _repository.Object,
            blacklistRepository.Object,
            userLookupService.Object,
            subscriptionService.Object,
            unreadCountersRepository.Object,
            identityProvider.Object,
            _intentionManager.Object,
            createBlogValidator.Object,
            updateBlogValidator.Object,
            createRubricValidator.Object,
            updateRubricValidator.Object,
            guidFactory.Object,
            dateTimeProvider.Object,
            _eventProducer.Object);
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
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateBlogEntity, CancellationToken>((update, _) => _capturedUpdate = update)
            .ReturnsAsync(blog);
        return blogId;
    }

    /// <summary>
    /// The caller holds no role in this blog: neither of the two view gates opens
    /// for them, which is what a stranger's read answers.
    /// </summary>
    private void HideEveryBlogFromTheCaller() =>
        _intentionManager
            .Setup(m => m.IsAllowed(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()))
            .Returns(false);

    /// <summary>
    /// The caller passes one of the two view gates and no other intention.
    /// </summary>
    private void OpenOnly(BlogIntention gate)
    {
        HideEveryBlogFromTheCaller();
        _intentionManager
            .Setup(m => m.IsAllowed(gate, It.IsAny<BlogDto>()))
            .Returns(true);
    }

    private async Task<HttpException> RefusedStart(string id)
    {
        var act = async () => await _service.ChangeStatusAsync(id, ModuleStatusTransition.Start);
        return (await act.Should().ThrowAsync<HttpException>()).Which;
    }

    #region Start

    [Fact]
    public async Task StartDraftBlogAndSetActivatedUtcOnFirstActivation()
    {
        var blogId = SetupBlog(ModuleStatus.Draft);

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        _capturedUpdate.Should().NotBeNull();
        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _capturedUpdate.ActivatedUtc.Should().Be(_now);
        _eventProducer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogActive)), blogId), Times.Once);
    }

    [Fact]
    public async Task StartDraftBlogWithoutOverwritingActivatedUtc()
    {
        var firstActivation = _now.AddMonths(-1);
        var blogId = SetupBlog(ModuleStatus.Draft, activatedUtc: firstActivation);

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _capturedUpdate.ActivatedUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(ModuleStatus.Active)]
    [InlineData(ModuleStatus.Closed)]
    public async Task RejectStartFromNonDraftStatus(ModuleStatus status)
    {
        var blogId = SetupBlog(status);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Freeze

    [Fact]
    public async Task FreezeActiveBlogWithFrozenReason()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Freeze);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.Frozen);
        _capturedUpdate.ClosedUtc.Should().Be(_now);
        _eventProducer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogFrozen)), blogId), Times.Once);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Closed)]
    public async Task RejectFreezeFromNonActiveStatus(ModuleStatus status)
    {
        var blogId = SetupBlog(status);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Freeze);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Finish

    [Fact]
    public async Task FinishActiveBlogWithFinishedReason()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Finish);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.Finished);
        _capturedUpdate.ClosedUtc.Should().Be(_now);
        _eventProducer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogFinished)), blogId), Times.Once);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Closed)]
    public async Task RejectFinishFromNonActiveStatus(ModuleStatus status)
    {
        var blogId = SetupBlog(status);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Finish);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Close

    [Fact]
    public async Task CloseActiveBlogWithNoneReasonAndSetClosedUtc()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Close);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        _capturedUpdate.ClosedUtc.Should().Be(_now);
        _eventProducer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogClosed)), blogId), Times.Once);
    }

    [Fact]
    public async Task CloseFrozenBlogWithoutOverwritingClosedUtc()
    {
        var frozenAt = _now.AddDays(-7);
        var blogId = SetupBlog(ModuleStatus.Closed, ClosedReason.Frozen, closedUtc: frozenAt);

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Close);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        _capturedUpdate.ClosedUtc.Should().BeNull();
    }

    [Fact]
    public async Task RejectCloseOfFinishedBlog()
    {
        var blogId = SetupBlog(ModuleStatus.Closed, ClosedReason.Finished, closedUtc: _now.AddDays(-7));

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Close);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectCloseOfDraftBlog()
    {
        var blogId = SetupBlog(ModuleStatus.Draft);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Close);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Reopen

    [Theory]
    [InlineData(ClosedReason.None)]
    [InlineData(ClosedReason.Finished)]
    [InlineData(ClosedReason.Frozen)]
    public async Task ReopenClosedBlogFromAnyReason(ClosedReason closedReason)
    {
        var blogId = SetupBlog(ModuleStatus.Closed, closedReason,
            activatedUtc: _now.AddMonths(-2), closedUtc: _now.AddDays(-7));

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Reopen);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        _capturedUpdate.ClearClosedUtc.Should().BeTrue();
        _capturedUpdate.ActivatedUtc.Should().BeNull(); // Already activated before
        _eventProducer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogActive)), blogId), Times.Once);
    }

    [Fact]
    public async Task ReopenNeverActivatedBlogAndSetActivatedUtc()
    {
        var blogId = SetupBlog(ModuleStatus.Closed, closedUtc: _now.AddDays(-7));

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Reopen);

        _capturedUpdate!.ActivatedUtc.Should().Be(_now);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Active)]
    public async Task RejectReopenFromNonClosedStatus(ModuleStatus status)
    {
        var blogId = SetupBlog(status);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Reopen);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Resolution and errors

    [Fact]
    public async Task ResolveBlogByPublicIdBeforeApplyingTransition()
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto { Id = blogId, Status = ModuleStatus.Draft };
        _repository.Setup(r => r.GetByPublicId("abcde", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateBlogEntity, CancellationToken>((update, _) => _capturedUpdate = update)
            .ReturnsAsync(blog);

        await _service.ChangeStatusAsync("abcde", ModuleStatusTransition.Start);

        _capturedUpdate!.BlogId.Should().Be(blogId);
        _capturedUpdate.Status.Should().Be(ModuleStatus.Active);
    }

    [Fact]
    public async Task RejectStatusChangeOfMissingBlogWithNotFound()
    {
        var blogId = Guid.NewGuid();
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogDto?)null);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    #endregion

    #region What the move may read

    /// <summary>
    /// A blog the caller cannot find anywhere on the site does not exist for them
    /// here either: the same status and the same message an unclaimed id gets,
    /// whichever of the two gates is the one that closed.
    /// </summary>
    /// <remarks>
    /// The endpoint is authentication-gated, because its moves belong to the blog
    /// leads and a lead is normally not moderation. Read without a scope it became
    /// an oracle over every private draft and every premoderated blog: 404 for "no
    /// such blog", 400 for "taken, and in the wrong status", 403 for "taken, and
    /// not yours". The game side never had it - GameService.ChangeStatusAsync
    /// reads through GetDetailsAsync under the accessibility scope, and a stranger
    /// is answered 404 before the machine sees the game.
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
        _repository.Setup(r => r.Get(missing, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogDto?)null);

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
        _repository.Setup(r => r.GetByPublicId("abcde", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hidden);
        _repository.Setup(r => r.GetByPublicId("fghij", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogDto?)null);

        var onTaken = await RefusedStart("abcde");
        var onUnclaimed = await RefusedStart("fghij");

        onTaken.StatusCode.Should().Be(HttpStatusCode.NotFound);
        onTaken.StatusCode.Should().Be(onUnclaimed.StatusCode);
        onTaken.Message.Should().Be(onUnclaimed.Message);
    }

    /// <summary>
    /// The refusal comes before anything else looks at the blog, so neither the
    /// state machine nor the authorization gate can leak what the 404 withheld.
    /// Start is legal from Draft and illegal from Active; a stranger is told the
    /// same thing either way.
    /// </summary>
    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Active)]
    public async Task RefuseAHiddenBlogBeforeTheStateMachineSeesIt(ModuleStatus status)
    {
        HideEveryBlogFromTheCaller();
        var blogId = SetupBlog(status, draftVisibility: DraftVisibility.Private);

        var refusal = await RefusedStart(blogId.ToString());

        refusal.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _intentionManager.Verify(
            m => m.ThrowIfForbidden(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()), Times.Never);
        _repository.Verify(
            r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), It.IsAny<CancellationToken>()), Times.Never);
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

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        _intentionManager.Verify(
            m => m.IsAllowed(BlogIntention.ViewDraft, It.IsAny<BlogDto>()), Times.Once);
        _intentionManager.Verify(
            m => m.IsAllowed(BlogIntention.ViewPremoderationPending, It.IsAny<BlogDto>()), Times.Once);
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

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
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

        await _service.ChangeStatusAsync(blogId.ToString(), ModuleStatusTransition.Start);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SetStatusActive, It.IsAny<BlogDto>()), Times.Once);
    }

    #endregion
}
