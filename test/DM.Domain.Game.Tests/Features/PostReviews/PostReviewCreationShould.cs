using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.PostReviews;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.PostReviews;

public class PostReviewCreationShould : PostReviewServiceTestBase
{
    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId);

        await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        intentionManager.Received(1).ThrowIfForbidden(PostReviewIntention.Create);
    }

    [Fact]
    public async Task ThrowNotFoundWhenPostDoesNotExist()
    {
        var postId = Guid.NewGuid();
        repository.GetPostInfoAsync(postId, currentUserId).Returns((PostInfo?)null);

        var act = async () => await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowForbiddenWhenReviewingOwnPost()
    {
        var postId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = currentUserId, GameId = gameId };

        repository.GetPostInfoAsync(postId, currentUserId).Returns(postInfo);

        var act = async () => await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowForbiddenWhenNewbieTryingToCreateNonNeutralReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };

        repository.GetPostInfoAsync(postId, currentUserId).Returns(postInfo);
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold - 1);

        var act = async () => await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AllowNewbieToCreateNeutralReview()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };
        var expectedReview = new PostReview { Id = Guid.NewGuid(), PostId = postId };

        repository.GetPostInfoAsync(postId, currentUserId).Returns(postInfo);
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold - 1);
        Reviewed();
        RecentlyReviewedGames();
        repository.CreateAsync(Arg.Any<CreatePostReviewEntity>()).Returns(expectedReview);

        var result = await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Neutral });

        result.Id.Should().Be(expectedReview.Id);
    }

    [Fact]
    public async Task ThrowConflictWhenReviewAlreadyExists()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };

        repository.GetPostInfoAsync(postId, currentUserId).Returns(postInfo);
        Reviewed(postId);
        RecentlyReviewedGames();

        var act = async () => await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    // The three-day window is a conflict with what the rater did in the game, not a rate limit: the
    // page that offered the form did not know of the review written since, and the reader is told
    // the rule in its own words.
    [Fact]
    public async Task ThrowConflictWithTheRuleWhenCooldownNotExpired()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };

        repository.GetPostInfoAsync(postId, currentUserId).Returns(postInfo);
        Reviewed();
        RecentlyReviewedGames(gameId);

        var act = async () => await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                        e.Message == "Оценивать посты в одной игре можно раз в три дня");
    }

    // Room access, applied where the post is read, is all that stands between a rating and a
    // private room.
    [Fact]
    public async Task ReadThePostAsTheRaterSoRoomAccessApplies()
    {
        var postId = Guid.NewGuid();
        SetupSuccessfulCreate(postId, Guid.NewGuid(), Guid.NewGuid());

        await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await repository.Received(1).GetPostInfoAsync(postId, currentUserId);
    }

    // QualityRating is stored and not summed, so the review and the delta are one write.
    [Fact]
    public async Task WriteAPositiveReviewForThePostAuthor()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId);

        await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Positive });

        await repository.Received(1).CreateAsync(Arg.Is<CreatePostReviewEntity>(
            e => e.PostAuthorId == postAuthorId && e.Sign == ReviewSign.Positive));
    }

    [Fact]
    public async Task WriteANegativeReviewForThePostAuthor()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId, ReviewSign.Negative);

        await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Negative });

        await repository.Received(1).CreateAsync(Arg.Is<CreatePostReviewEntity>(
            e => e.PostAuthorId == postAuthorId && e.Sign == ReviewSign.Negative));
    }

    [Fact]
    public async Task WriteANeutralReviewForThePostAuthor()
    {
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        SetupSuccessfulCreate(postId, postAuthorId, gameId, ReviewSign.Neutral);

        await service.CreateAsync(new CreatePostReview { PostId = postId, Sign = ReviewSign.Neutral });

        await repository.Received(1).CreateAsync(
            Arg.Is<CreatePostReviewEntity>(e => e.Sign == ReviewSign.Neutral));
    }

    // The flag rides on every page of posts, so a guest, whom the intention already refuses, costs
    // no query at all.
    [Fact]
    public async Task AnswerAGuestWithoutAskingTheDatabase()
    {
        intentionManager.IsAllowed(PostReviewIntention.Create).Returns(false);

        var result = await service.ReviewableAsync([new ReviewCandidate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())]);

        result.Should().BeEmpty();
        await repository.DidNotReceiveWithAnyArgs().SelectReviewedPostIdsAsync(default, default!);
        await repository.DidNotReceiveWithAnyArgs().SelectGamesReviewedSinceAsync(default, default!, default);
    }

    [Fact]
    public async Task JudgeAPageOfPostsByTheRulesOfTheWriteInTwoQueries()
    {
        intentionManager.IsAllowed(PostReviewIntention.Create).Returns(true);
        var game = Guid.NewGuid();
        var coolingGame = Guid.NewGuid();
        var own = new ReviewCandidate(Guid.NewGuid(), currentUserId, game);
        var rated = new ReviewCandidate(Guid.NewGuid(), Guid.NewGuid(), game);
        var cooling = new ReviewCandidate(Guid.NewGuid(), Guid.NewGuid(), coolingGame);
        var open = new ReviewCandidate(Guid.NewGuid(), Guid.NewGuid(), game);
        Reviewed(rated.PostId);
        RecentlyReviewedGames(coolingGame);

        var result = await service.ReviewableAsync([own, rated, cooling, open]);

        result.Should().BeEquivalentTo([open.PostId]);
        await repository.Received(1).SelectReviewedPostIdsAsync(currentUserId, Arg.Any<IReadOnlyCollection<Guid>>());
        await repository.Received(1).SelectGamesReviewedSinceAsync(currentUserId,
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTimeOffset>());
    }

    private void SetupSuccessfulCreate(Guid postId, Guid postAuthorId, Guid gameId, ReviewSign sign = ReviewSign.Positive)
    {
        var postInfo = new PostInfo { AuthorId = postAuthorId, GameId = gameId };
        var expectedReview = new PostReview
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            Sign = sign,
            PostAuthor = new GeneralUser { UserId = postAuthorId }
        };

        repository.GetPostInfoAsync(postId, currentUserId).Returns(postInfo);
        Reviewed();
        RecentlyReviewedGames();
        repository.CreateAsync(Arg.Any<CreatePostReviewEntity>()).Returns(expectedReview);
    }
}
