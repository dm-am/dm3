using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Statuses;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Testing;
using DM.Testing.Dsl;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Domain.Blog.Tests.Features.Blogs;

/// <summary>
/// The premoderation half of the blog service: the status a blog is created in,
/// and the three moves that change it afterwards.
/// </summary>
/// <remarks>
/// Mirrors the premoderation region of GameStatusTransitionShould one for one.
/// The blog side had no service-level premoderation coverage at all while the
/// machine was already shared, so the game tests were quietly standing in for
/// both and only the game half of any change was ever proved.
/// </remarks>
public class BlogPremoderationShould : UnitTestBase
{
    private readonly Mock<IBlogRepository> _repository;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly BlogService _service;
    private readonly AuthenticatedUser _author;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private UpdateBlogEntity? _capturedUpdate;
    private CreateBlogEntity? _capturedCreate;

    public BlogPremoderationShould()
    {
        _repository = Mock<IBlogRepository>();
        var blacklistRepository = Mock<IBlogBlacklistRepository>();
        var userLookupService = Mock<IUserLookupService>();
        var subscriptionService = Mock<IBlogSubscriptionService>();
        var unreadCountersRepository = Mock<IUnreadCountersRepository>();

        // An established author by default: past the newbie threshold and not
        // watched. The creation tests move these two fields and nothing else.
        var identity = Identities.User(_currentUserId, UserRole.RegularUser);
        _author = identity.User;
        _author.QuantityRating = ProbationPolicy.NewbiePostThreshold;

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(identity);

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<BlogIntention>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()));
        // Every intention says yes by default, which is the same answer the no-op
        // ThrowIfForbidden above gives. The reads that ask instead of throwing —
        // the visibility gates of the author's move — have to get that same answer,
        // or every test here would be reading as a stranger. The ones that mean to
        // be a stranger call HideEveryBlogFromTheCaller.
        _intentionManager
            .Setup(m => m.IsAllowed(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()))
            .Returns(true);

        var createBlogValidator = Mock<IValidator<CreateBlog>>();
        createBlogValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateBlog>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
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

        _repository.Setup(r => r.CreateBlog(It.IsAny<CreateBlogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateBlogEntity, CancellationToken>((entity, _) => _capturedCreate = entity)
            .ReturnsAsync((CreateBlogEntity entity, CancellationToken _) => new BlogDto
            {
                Id = entity.BlogId,
                Author = new GeneralUser { UserId = entity.OwnerId },
                PremoderationStatus = entity.PremoderationStatus
            });

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

    private Guid SetupBlog(PremoderationStatus premoderationStatus, Guid? ownerId = null)
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Status = ModuleStatus.Draft,
            PremoderationStatus = premoderationStatus,
            Author = new GeneralUser { UserId = ownerId ?? _currentUserId }
        };
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blog);
        _repository.Setup(r => r.UpdateBlog(It.IsAny<UpdateBlogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateBlogEntity, CancellationToken>((update, _) => _capturedUpdate = update)
            .ReturnsAsync(blog);
        return blogId;
    }

    /// <summary>
    /// The caller holds no role in any blog: neither of the two view gates opens
    /// for them, which is what a stranger's read answers.
    /// </summary>
    private void HideEveryBlogFromTheCaller() =>
        _intentionManager
            .Setup(m => m.IsAllowed(It.IsAny<BlogIntention>(), It.IsAny<BlogDto>()))
            .Returns(false);

    private async Task<HttpException> RefusedSubmit(Guid blogId)
    {
        var act = async () => await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);
        return (await act.Should().ThrowAsync<HttpException>()).Which;
    }

    #region Moderation verdicts

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task ApproveABlogFromAnyStatusAndClearTheCurator(PremoderationStatus current)
    {
        var blogId = SetupBlog(current);

        await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SetApproved);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.Approved);
        _capturedUpdate.MentorId.Should().BeNull();
        _capturedUpdate.SetMentorId.Should().BeTrue();
        _eventProducer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogModeration)), blogId), Times.Once);
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task ReturnABlogForEditsFromAnyStatusAndRecordTheCurator(PremoderationStatus current)
    {
        var blogId = SetupBlog(current);

        await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SetAwaitingEdits);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingEdits);
        _capturedUpdate.MentorId.Should().Be(_currentUserId);
        _capturedUpdate.SetMentorId.Should().BeTrue();
    }

    /// <summary>
    /// The two verdicts are a rank and nothing else, so they are asked of the
    /// targetless intention — before the blog is read.
    /// </summary>
    [Theory]
    [InlineData(ModulePremoderationTransition.SetApproved)]
    [InlineData(ModulePremoderationTransition.SetAwaitingEdits)]
    public async Task AskTheMentorRankForAVerdictAndNothingAboutTheBlog(
        ModulePremoderationTransition transition)
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(blogId.ToString(), transition);

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SetStatusModeration), Times.Once);
        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SubmitForApproval, It.IsAny<BlogDto>()), Times.Never);
    }

    [Fact]
    public async Task RejectAPremoderationMoveOnAMissingBlogWithNotFound()
    {
        var blogId = Guid.NewGuid();
        _repository.Setup(r => r.Get(blogId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogDto?)null);

        var act = async () => await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SetApproved);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    #endregion

    #region The author's move

    [Fact]
    public async Task SubmitABlogAwaitingEditsForApprovalWithoutTouchingTheCurator()
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        _capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingApproval);
        _capturedUpdate.SetMentorId.Should().BeFalse();
        _eventProducer.Verify(p => p.SendAsync(
            It.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogModeration)), blogId), Times.Once);
    }

    /// <summary>
    /// The author's move is a fact about the blog, so it is asked of the targeted
    /// intention — and the mentor rank is not asked at all, because the owner
    /// making this move normally does not hold it.
    /// </summary>
    [Fact]
    public async Task AskTheBlogWhoTheAuthorIsAndNotTheMentorRank()
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SubmitForApproval, It.IsAny<BlogDto>()), Times.Once);
        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SetStatusModeration), Times.Never);
    }

    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    public async Task RejectSubmitForApprovalWhenNotAwaitingEdits(PremoderationStatus current)
    {
        var blogId = SetupBlog(current);

        var act = async () => await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Illegality is answered before authorization, as on the status machine: the
    /// author gets a 400 naming the move, not a 403 about who they are.
    /// </summary>
    [Fact]
    public async Task RefuseAnIllegalSubmitBeforeAskingWhoTheCallerIs()
    {
        var blogId = SetupBlog(PremoderationStatus.Approved);

        var act = async () => await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        await act.Should().ThrowAsync<HttpException>();
        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SubmitForApproval, It.IsAny<BlogDto>()), Times.Never);
    }

    #endregion

    #region What the author's move may read

    /// <summary>
    /// A blog the caller cannot find anywhere on the site does not exist for them
    /// here either: the same status and the same message an unclaimed alias gets,
    /// whatever state the blog behind the alias is in.
    /// </summary>
    /// <remarks>
    /// The endpoint is authentication-gated rather than Mentor-gated, because the
    /// owner making this move is normally neither mentor nor moderator. Read
    /// without a scope it became a three-valued oracle over every private draft
    /// and every premoderated blog on the site: 404 for "no such alias", 400 for
    /// "taken, and not awaiting edits", 403 for "taken, and awaiting edits". The
    /// game side never had it — its author's move reads through GetGameDetails
    /// under the ordinary accessibility scope, and a stranger is answered 404
    /// before the state machine sees the game.
    /// </remarks>
    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task AnswerOnAHiddenBlogTheWayItAnswersOnNoBlogAtAll(PremoderationStatus current)
    {
        HideEveryBlogFromTheCaller();
        var hidden = SetupBlog(current, ownerId: Guid.NewGuid());
        var missing = Guid.NewGuid();
        _repository.Setup(r => r.Get(missing, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BlogDto?)null);

        var onHidden = await RefusedSubmit(hidden);
        var onMissing = await RefusedSubmit(missing);

        onHidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        onHidden.StatusCode.Should().Be(onMissing.StatusCode);
        onHidden.Message.Should().Be(onMissing.Message);
    }

    /// <summary>
    /// And it is answered before anything else looks at the blog, so neither the
    /// state machine nor the authorization gate can leak what the 404 withheld.
    /// </summary>
    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task RefuseAHiddenBlogBeforeTheStateMachineSeesIt(PremoderationStatus current)
    {
        HideEveryBlogFromTheCaller();
        var blogId = SetupBlog(current, ownerId: Guid.NewGuid());

        await RefusedSubmit(blogId);

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SubmitForApproval, It.IsAny<BlogDto>()), Times.Never);
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
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await _service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        _intentionManager.Verify(
            m => m.IsAllowed(BlogIntention.ViewDraft, It.IsAny<BlogDto>()), Times.Once);
        _intentionManager.Verify(
            m => m.IsAllowed(BlogIntention.ViewPremoderationPending, It.IsAny<BlogDto>()), Times.Once);
    }

    /// <summary>
    /// The verdict reads past all of that, and has to: the blogs it is passed on
    /// are exactly the ones nobody outside them may open. The rank is what pays
    /// for it, and it was asked before the blog was read.
    /// </summary>
    [Theory]
    [InlineData(ModulePremoderationTransition.SetApproved)]
    [InlineData(ModulePremoderationTransition.SetAwaitingEdits)]
    public async Task LetAVerdictThroughOnABlogTheJudgeCouldNotOtherwiseOpen(
        ModulePremoderationTransition transition)
    {
        HideEveryBlogFromTheCaller();
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits, ownerId: Guid.NewGuid());

        await _service.ChangePremoderationAsync(blogId.ToString(), transition);

        _capturedUpdate.Should().NotBeNull();
        _intentionManager.Verify(
            m => m.ThrowIfForbidden(BlogIntention.SetStatusModeration), Times.Once);
    }

    #endregion

    #region Status at creation

    /// <summary>
    /// All four combinations of the two things that hold an author back.
    /// </summary>
    [Theory]
    [InlineData(false, false, PremoderationStatus.Approved)]
    [InlineData(true, false, PremoderationStatus.AwaitingEdits)]
    [InlineData(false, true, PremoderationStatus.AwaitingEdits)]
    [InlineData(true, true, PremoderationStatus.AwaitingEdits)]
    public async Task CreateABlogInTheStatusItsAuthorEarns(
        bool newbie, bool underWatch, PremoderationStatus expected)
    {
        _author.QuantityRating = newbie ? 0 : ProbationPolicy.NewbiePostThreshold;
        _author.IsUnderModerationWatch = underWatch;

        await _service.Create(new CreateBlog { Title = "Blog" });

        _capturedCreate!.PremoderationStatus.Should().Be(expected);
    }

    #endregion
}
