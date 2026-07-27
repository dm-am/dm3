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
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
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

        var intentionManager = Mock<IIntentionManager>();
        intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<BlogIntention>()));
        intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()));

        var createBlogValidator = Mock<IValidator<CreateBlog>>();
        var updateBlogValidator = Mock<IValidator<UpdateBlog>>();
        var createRubricValidator = Mock<IValidator<CreateRubric>>();
        var updateRubricValidator = Mock<IValidator<UpdateRubric>>();
        var createPublicationValidator = Mock<IValidator<CreatePublication>>();
        var updatePublicationValidator = Mock<IValidator<UpdatePublication>>();

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
            intentionManager.Object,
            createBlogValidator.Object,
            updateBlogValidator.Object,
            createRubricValidator.Object,
            updateRubricValidator.Object,
            createPublicationValidator.Object,
            updatePublicationValidator.Object,
            guidFactory.Object,
            dateTimeProvider.Object,
            _eventProducer.Object);
    }

    private Guid SetupBlog(
        ModuleStatus status,
        ClosedReason closedReason = ClosedReason.None,
        DateTimeOffset? activatedUtc = null,
        DateTimeOffset? closedUtc = null)
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Status = status,
            ClosedReason = closedReason,
            ActivatedUtc = activatedUtc,
            ClosedUtc = closedUtc
        };
        _repository.Setup(r => r.Get(blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateBlogEntity, CancellationToken>((update, _) => _capturedUpdate = update)
            .ReturnsAsync(blog);
        return blogId;
    }

    #region Start

    [Fact]
    public async Task StartDraftBlogAndSetActivatedUtcOnFirstActivation()
    {
        var blogId = SetupBlog(ModuleStatus.Draft);

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Start);

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

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Start);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        _capturedUpdate.ActivatedUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(ModuleStatus.Active)]
    [InlineData(ModuleStatus.Closed)]
    public async Task RejectStartFromNonDraftStatus(ModuleStatus status)
    {
        var blogId = SetupBlog(status);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Freeze

    [Fact]
    public async Task FreezeActiveBlogWithFrozenReason()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Freeze);

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

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Freeze);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Finish

    [Fact]
    public async Task FinishActiveBlogWithFinishedReason()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Finish);

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

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Finish);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    #endregion

    #region Close

    [Fact]
    public async Task CloseActiveBlogWithNoneReasonAndSetClosedUtc()
    {
        var blogId = SetupBlog(ModuleStatus.Active);

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Close);

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

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Close);

        _capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        _capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        _capturedUpdate.ClosedUtc.Should().BeNull();
    }

    [Fact]
    public async Task RejectCloseOfFinishedBlog()
    {
        var blogId = SetupBlog(ModuleStatus.Closed, ClosedReason.Finished, closedUtc: _now.AddDays(-7));

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Close);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectCloseOfDraftBlog()
    {
        var blogId = SetupBlog(ModuleStatus.Draft);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Close);

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

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Reopen);

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

        await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Reopen);

        _capturedUpdate!.ActivatedUtc.Should().Be(_now);
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Active)]
    public async Task RejectReopenFromNonClosedStatus(ModuleStatus status)
    {
        var blogId = SetupBlog(status);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Reopen);

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
        _repository.Setup(r => r.GetByPublicId("abcde", It.IsAny<CancellationToken>()))
            .ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateBlogEntity, CancellationToken>((update, _) => _capturedUpdate = update)
            .ReturnsAsync(blog);

        await _service.ChangeStatusAsync("abcde", BlogStatusTransition.Start);

        _capturedUpdate!.BlogId.Should().Be(blogId);
        _capturedUpdate.Status.Should().Be(ModuleStatus.Active);
    }

    [Fact]
    public async Task RejectStatusChangeOfMissingBlogWithNotFound()
    {
        var blogId = Guid.NewGuid();
        _repository.Setup(r => r.Get(blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogDto?)null);

        var act = async () => await _service.ChangeStatusAsync(blogId.ToString(), BlogStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    #endregion
}
