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
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

/// <summary>
/// Blog status state machine tests. Mirror GameStatusTransitionShould
/// one-to-one: the same transitions, the same legality rules, the same
/// ActivatedUtc / ClosedUtc / ClosedReason handling.
/// </summary>
public class BlogStatusTransitionShould : UnitTestBase
{
    private readonly IBlogRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IEventProducer _eventProducer;
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
        identityProvider.Current.Returns(Identity.Guest());

        _intentionManager = Mock<IIntentionManager>();
        // Every intention says yes by default: a substituted ThrowIfForbidden returns
        // void and does nothing. The visibility gates ask instead of throwing, so
        // without this the whole machine would be exercised as a stranger and answer
        // 404 everywhere. The tests that mean to be a stranger call
        // HideEveryBlogFromTheCaller.
        _intentionManager
            .IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(true);

        var createBlogValidator = Mock<IValidator<CreateBlog>>();
        var updateBlogValidator = Mock<IValidator<UpdateBlog>>();
        var createRubricValidator = Mock<IValidator<CreateRubric>>();
        var updateRubricValidator = Mock<IValidator<UpdateRubric>>();

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(_now);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);
        _eventProducer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _service = new BlogService(
            _repository,
            blacklistRepository,
            userLookupService,
            subscriptionService,
            unreadCountersRepository,
            identityProvider,
            _intentionManager,
            createBlogValidator,
            updateBlogValidator,
            createRubricValidator,
            updateRubricValidator,
            guidFactory,
            dateTimeProvider,
            _eventProducer);
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
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        _repository.UpdateBlog(Arg.Any<UpdateBlogEntity>(), Arg.Any<CancellationToken>())
            .Returns(blog)
            .AndDoes(ci =>
            {
                var update = ci.ArgAt<UpdateBlogEntity>(0);
                _capturedUpdate = update;
            });
        return blogId;
    }

    /// <summary>
    /// The caller holds no role in this blog: neither of the two view gates opens
    /// for them, which is what a stranger's read answers.
    /// </summary>
    private void HideEveryBlogFromTheCaller() =>
        _intentionManager
            .IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(false);

    /// <summary>
    /// The caller passes one of the two view gates and no other intention.
    /// </summary>
    private void OpenOnly(BlogIntention gate)
    {
        HideEveryBlogFromTheCaller();
        _intentionManager
            .IsAllowed(gate, Arg.Any<BlogDto>()).Returns(true);
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
        await _eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogActive)), blogId);
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
        await _eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogFrozen)), blogId);
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
        await _eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogFinished)), blogId);
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
        await _eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogClosed)), blogId);
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
        await _eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogActive)), blogId);
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
        _repository.GetByPublicId("abcde", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        _repository.UpdateBlog(Arg.Any<UpdateBlogEntity>(), Arg.Any<CancellationToken>())
            .Returns(blog)
            .AndDoes(ci =>
            {
                var update = ci.ArgAt<UpdateBlogEntity>(0);
                _capturedUpdate = update;
            });

        await _service.ChangeStatusAsync("abcde", ModuleStatusTransition.Start);

        _capturedUpdate!.BlogId.Should().Be(blogId);
        _capturedUpdate.Status.Should().Be(ModuleStatus.Active);
    }

    [Fact]
    public async Task RejectStatusChangeOfMissingBlogWithNotFound()
    {
        var blogId = Guid.NewGuid();
        _repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

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
        _repository.Get(missing, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

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
        _repository.GetByPublicId("abcde", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hidden);
        _repository.GetByPublicId("fghij", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

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
        _intentionManager.DidNotReceive().ThrowIfForbidden(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>());
        await _repository.DidNotReceive().UpdateBlog(Arg.Any<UpdateBlogEntity>(), Arg.Any<CancellationToken>());
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

        _intentionManager.Received(1).IsAllowed(BlogIntention.ViewDraft, Arg.Any<BlogDto>());
        _intentionManager.Received(1).IsAllowed(BlogIntention.ViewPremoderationPending, Arg.Any<BlogDto>());
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
        _intentionManager.Received(1).ThrowIfForbidden(BlogIntention.SetStatusActive, Arg.Any<BlogDto>());
    }

    #endregion
}
