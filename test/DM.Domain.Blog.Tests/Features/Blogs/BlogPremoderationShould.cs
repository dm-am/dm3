using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Statuses;
using DM.Domain.Blog.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Domain.Blog.Tests.Features.Blogs;

// One case a move: the table itself is ModulePremoderationPolicyShould.
public class BlogPremoderationShould : BlogServiceTestBase
{
    private readonly AuthenticatedUser author;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private UpdateBlogEntity? capturedUpdate;
    private CreateBlogEntity? capturedCreate;

    public BlogPremoderationShould()
    {
        // An established author by default: past the newbie threshold and not
        // watched. The creation tests move these two fields and nothing else.
        var identity = Identities.User(currentUserId, UserRole.RegularUser);
        author = identity.User;
        author.QuantityRating = ProbationPolicy.NewbiePostThreshold;
        identityProvider.Current.Returns(identity);

        // Every intention says yes by default, a test that means a stranger calls HideEveryBlogFromTheCaller.
        intentionManager
            .IsAllowed(Arg.Any<BlogIntention>(), Arg.Any<BlogDto>()).Returns(true);
        guidFactory.Create().Returns(Guid.NewGuid());
        dateTimeProvider.Now.Returns(now);

        repository.CreateBlog(Arg.Any<CreateBlogEntity>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var entity = ci.ArgAt<CreateBlogEntity>(0);
                return new BlogDto
                {
                    Id = entity.BlogId,
                    Author = new GeneralUser { UserId = entity.OwnerId },
                    PremoderationStatus = entity.PremoderationStatus
                };
            })
            .AndDoes(ci => capturedCreate = ci.ArgAt<CreateBlogEntity>(0));
    }

    private Guid SetupBlog(PremoderationStatus premoderationStatus, Guid? ownerId = null)
    {
        var blogId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Status = ModuleStatus.Draft,
            PremoderationStatus = premoderationStatus,
            Author = new GeneralUser { UserId = ownerId ?? currentUserId }
        };
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);
        repository.UpdateBlog(Arg.Do<UpdateBlogEntity>(update => capturedUpdate = update), Arg.Any<CancellationToken>())
            .Returns(blog);
        return blogId;
    }

    private async Task<HttpException> RefusedSubmit(Guid blogId)
    {
        var act = async () => await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);
        return (await act.Should().ThrowAsync<HttpException>()).Which;
    }

    [Fact]
    public async Task ApproveABlogAndClearTheCurator()
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingApproval);

        await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SetApproved);

        capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.Approved);
        capturedUpdate.MentorId.Should().BeNull();
        capturedUpdate.SetMentorId.Should().BeTrue();
        await eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogModeration)), blogId);
    }

    [Fact]
    public async Task ReturnABlogForEditsAndRecordTheCurator()
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingApproval);

        await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SetAwaitingEdits);

        capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingEdits);
        capturedUpdate.MentorId.Should().Be(currentUserId);
        capturedUpdate.SetMentorId.Should().BeTrue();
    }

    // The two verdicts are a rank and nothing else, so they are asked of the targetless intention —
    // before the blog is read.
    [Theory]
    [InlineData(ModulePremoderationTransition.SetApproved)]
    [InlineData(ModulePremoderationTransition.SetAwaitingEdits)]
    public async Task AskTheMentorRankForAVerdictAndNothingAboutTheBlog(
        ModulePremoderationTransition transition)
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await service.ChangePremoderationAsync(blogId.ToString(), transition);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.SetStatusModeration);
        intentionManager.DidNotReceive().ThrowIfForbidden(BlogIntention.SubmitForApproval, Arg.Any<BlogDto>());
    }

    [Fact]
    public async Task RejectAPremoderationMoveOnAMissingBlogWithNotFound()
    {
        var blogId = Guid.NewGuid();
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

        var act = async () => await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SetApproved);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitABlogAwaitingEditsForApprovalWithoutTouchingTheCurator()
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingApproval);
        capturedUpdate.SetMentorId.Should().BeFalse();
        await eventProducer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusBlogModeration)), blogId);
    }

    // The author's move is a fact about the blog, so it is asked of the targeted intention — and
    // the mentor rank is not asked at all, because the owner making this move normally does not
    // hold it.
    [Fact]
    public async Task AskTheBlogWhoTheAuthorIsAndNotTheMentorRank()
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.SubmitForApproval, Arg.Any<BlogDto>());
        intentionManager.DidNotReceive().ThrowIfForbidden(BlogIntention.SetStatusModeration);
    }

    // Illegality is answered before authorization, as on the status machine: the author gets a 400
    // naming the move, not a 403 about who they are.
    [Fact]
    public async Task RefuseAnIllegalSubmitBeforeAskingWhoTheCallerIs()
    {
        var blogId = SetupBlog(PremoderationStatus.Approved);

        var act = async () => await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        await act.Should().ThrowAsync<HttpException>();
        intentionManager.DidNotReceive().ThrowIfForbidden(BlogIntention.SubmitForApproval, Arg.Any<BlogDto>());
    }

    // Read without a scope, the codes 404, 400 and 403 would tell a stranger which hidden blogs
    // exist.
    [Theory]
    [InlineData(PremoderationStatus.Approved)]
    [InlineData(PremoderationStatus.AwaitingApproval)]
    [InlineData(PremoderationStatus.AwaitingEdits)]
    public async Task AnswerOnAHiddenBlogTheWayItAnswersOnNoBlogAtAll(PremoderationStatus current)
    {
        HideEveryBlogFromTheCaller();
        var hidden = SetupBlog(current, ownerId: Guid.NewGuid());
        var missing = Guid.NewGuid();
        repository.Get(missing, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BlogDto?)null);

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

        intentionManager.DidNotReceive().ThrowIfForbidden(BlogIntention.SubmitForApproval, Arg.Any<BlogDto>());
        await repository.DidNotReceive().UpdateBlog(Arg.Any<UpdateBlogEntity>(), Arg.Any<CancellationToken>());
    }

    // The scope is the reader's ordinary one and nothing invented for this endpoint: the same two
    // gates a plain read applies, the private draft and the pending verdict.
    [Fact]
    public async Task ReadTheBlogUnderTheTwoGatesAnOrdinaryReadApplies()
    {
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits);

        await service.ChangePremoderationAsync(
            blogId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        intentionManager.Received(1).IsAllowed(BlogIntention.ViewDraft, Arg.Any<BlogDto>());
        intentionManager.Received(1).IsAllowed(BlogIntention.ViewPremoderationPending, Arg.Any<BlogDto>());
    }

    // The verdict reads past all of that, and has to: the blogs it is passed on are exactly the
    // ones nobody outside them may open.
    [Theory]
    [InlineData(ModulePremoderationTransition.SetApproved)]
    [InlineData(ModulePremoderationTransition.SetAwaitingEdits)]
    public async Task LetAVerdictThroughOnABlogTheJudgeCouldNotOtherwiseOpen(
        ModulePremoderationTransition transition)
    {
        HideEveryBlogFromTheCaller();
        var blogId = SetupBlog(PremoderationStatus.AwaitingEdits, ownerId: Guid.NewGuid());

        await service.ChangePremoderationAsync(blogId.ToString(), transition);

        capturedUpdate.Should().NotBeNull();
        intentionManager.Received(1).ThrowIfForbidden(BlogIntention.SetStatusModeration);
    }

    [Theory]
    [InlineData(false, false, PremoderationStatus.Approved)]
    [InlineData(true, false, PremoderationStatus.AwaitingEdits)]
    [InlineData(false, true, PremoderationStatus.AwaitingEdits)]
    [InlineData(true, true, PremoderationStatus.AwaitingEdits)]
    public async Task CreateABlogInTheStatusItsAuthorEarns(
        bool newbie, bool underWatch, PremoderationStatus expected)
    {
        author.QuantityRating = newbie ? 0 : ProbationPolicy.NewbiePostThreshold;
        author.IsUnderModerationWatch = underWatch;

        await service.Create(new CreateBlog { Title = "Blog" });

        capturedCreate!.PremoderationStatus.Should().Be(expected);
    }
}
