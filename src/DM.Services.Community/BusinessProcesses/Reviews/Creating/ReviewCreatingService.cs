using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Community.BusinessProcesses.Reviews.Validation;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <inheritdoc />
internal class ReviewCreatingService : IReviewCreatingService
{
    private readonly IValidator<CreateReview> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IReviewFactory _factory;
    private readonly IReviewCreatingRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserReadingService _userReadingService;
    private readonly IReviewEligibilityService _eligibilityService;
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public ReviewCreatingService(
        IValidator<CreateReview> validator,
        IIntentionManager intentionManager,
        IReviewFactory factory,
        IReviewCreatingRepository repository,
        IIdentityProvider identityProvider,
        IUserReadingService userReadingService,
        IReviewEligibilityService eligibilityService,
        DmDbContext dbContext)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _identityProvider = identityProvider;
        _userReadingService = userReadingService;
        _eligibilityService = eligibilityService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Review> Create(CreateReview createReview)
    {
        await _validator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(ReviewIntention.Create);

        var authorId = _identityProvider.Current.User.UserId;
        if (!string.IsNullOrEmpty(createReview.AuthorLogin))
        {
            var author = await _userReadingService.Get(createReview.AuthorLogin);
            authorId = author.UserId;
        }

        if (await _repository.UserHasReview(authorId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already has a review");
        }

        var review = _factory.Create(createReview, authorId, isApproved: true);
        try
        {
            return await _repository.Create(review);
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already has a review");
        }
    }

    /// <inheritdoc />
    public async Task<Review> CreateUserReview(Guid targetUserId, string text)
    {
        _intentionManager.ThrowIfForbidden(ReviewIntention.CreateUserReview);

        var authorId = _identityProvider.Current.User.UserId;

        // Newbies cannot create user reviews
        if (await _eligibilityService.IsNewbie(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You need at least 100 game posts to create user reviews");
        }

        // Can't review yourself
        if (authorId == targetUserId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You cannot review yourself");
        }

        // Check if users have played together
        var havePlayedTogether = await _eligibilityService.HavePlayedTogether(authorId, targetUserId);
        if (!havePlayedTogether)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You can only review users you have played with in the same game");
        }

        // Check if already reviewed
        if (await _eligibilityService.HasUserReview(authorId, targetUserId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this user");
        }

        var createReview = new CreateReview
        {
            Text = text,
            TargetType = ReviewTargetType.User,
            TargetId = targetUserId
        };

        var review = _factory.Create(createReview, authorId, isApproved: true);
        try
        {
            return await _repository.Create(review);
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this user");
        }
    }

    /// <inheritdoc />
    public async Task<Review> CreateGameReview(Guid gameId, string text)
    {
        _intentionManager.ThrowIfForbidden(ReviewIntention.CreateGameReview);

        var authorId = _identityProvider.Current.User.UserId;

        // Newbies cannot create game reviews
        if (await _eligibilityService.IsNewbie(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You need at least 100 game posts to create game reviews");
        }

        // Check if user can review this game (has at least one post in the game)
        var canReview = await _eligibilityService.CanReviewGame(authorId, gameId);
        if (!canReview)
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You can only review games where you have at least one post");
        }

        // Check if already reviewed
        if (await _eligibilityService.HasGameReview(authorId, gameId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this game");
        }

        var createReview = new CreateReview
        {
            Text = text,
            TargetType = ReviewTargetType.Game,
            TargetId = gameId
        };

        var review = _factory.Create(createReview, authorId, isApproved: true);
        try
        {
            return await _repository.Create(review);
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this game");
        }
    }

    /// <inheritdoc />
    public async Task<Review> CreatePostReview(Guid postId, ReviewSign sign, ReviewReasonType? reasonType = null)
    {
        _intentionManager.ThrowIfForbidden(ReviewIntention.CreatePostReview);

        var authorId = _identityProvider.Current.User.UserId;

        // Get post information for authorization and denormalization
        var postInfo = await _repository.GetPostInfo(postId);
        if (postInfo == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Post not found");
        }

        // Can't review own post
        if (authorId == postInfo.AuthorId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You cannot review your own post");
        }

        // Newbies can only create neutral post reviews
        if (sign != ReviewSign.Neutral && await _eligibilityService.IsNewbie(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                "You need at least 100 game posts to create positive or negative reviews");
        }

        // Check if already reviewed this post
        var existingReview = await _repository.GetPostReviewByAuthor(postId, authorId);
        if (existingReview != null)
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this post");
        }

        // Check cooldown: can't review posts in the same game within 3 days
        if (await _eligibilityService.HasRecentPostReviewInGame(authorId, postInfo.GameId))
        {
            throw new HttpException(HttpStatusCode.TooManyRequests,
                "You can only submit one post review per game every 3 days");
        }

        var dbReview = _factory.CreatePostReview(
            postId: postId,
            authorId: authorId,
            postAuthorId: postInfo.AuthorId,
            gameId: postInfo.GameId,
            sign: sign,
            reasonType: reasonType);

        try
        {
            var result = await _repository.Create(dbReview);

            // Update post author's QualityRating based on review sign
            if (sign != ReviewSign.Neutral)
            {
                var signValue = (int)sign;
                await _dbContext.Users
                    .Where(u => u.UserId == postInfo.AuthorId)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.QualityRating, x => x.QualityRating + signValue));
            }

            return result;
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "You have already reviewed this post");
        }
    }
}