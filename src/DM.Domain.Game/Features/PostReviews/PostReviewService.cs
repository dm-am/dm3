using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.PostReviews;

/// <inheritdoc />
internal class PostReviewService : IPostReviewService
{
    /// <summary>
    /// How long the author may correct what they published. The same quarter of
    /// an hour a game post and a comment give their author (PostIntentionResolver,
    /// CommentIntentionResolver and the two clients that mirror them): an opinion
    /// somebody else has already read is not a draft, and the correction window
    /// is for a typo, not for a change of mind. Deleting is the way out after it
    /// closes, and it has no window.
    /// </summary>
    private static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan CooldownPerGame = TimeSpan.FromDays(3);

    private const string ReviewNotFound = "Оценка не найдена";

    private readonly IValidator<CreatePostReview> _createValidator;
    private readonly IValidator<UpdatePostReview> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IPostReviewRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGameBlacklistRepository _blacklistRepository;

    public PostReviewService(
        IValidator<CreatePostReview> createValidator,
        IValidator<UpdatePostReview> updateValidator,
        IIntentionManager intentionManager,
        IPostReviewRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IGameBlacklistRepository blacklistRepository)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _blacklistRepository = blacklistRepository;
    }

    /// <inheritdoc />
    public async Task<PostReview> CreateAsync(CreatePostReview createReview)
    {
        await _createValidator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(PostReviewIntention.Create);

        var author = _identityProvider.Current.User;
        var authorId = author.UserId;
        var postId = createReview.PostId;

        // Get post information for authorization and denormalization
        var postInfo = await _repository.GetPostInfoAsync(postId, authorId);
        if (postInfo == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        // The blacklist closes writing, and rating a post writes into the game it
        // belongs to. The game's rooms and posts are open to a blacklisted reader
        // and stay open, so the refusal has to be here and not in what they see.
        if (await _blacklistRepository.IsBlocked(postInfo.GameId, authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }

        // Can't review own post
        if (authorId == postInfo.AuthorId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя оценивать собственный пост");
        }

        // Newbies can only create neutral post reviews
        if (createReview.Sign != ReviewSign.Neutral && await IsNewbieAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                RefusalMessage.SignedReviewNeedsExperience(ProbationPolicy.NewbiePostThreshold));
        }

        // Check if already reviewed this post
        if (await ExistsAsync(authorId, postId))
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedPost);
        }

        // Check cooldown: can't review posts in the same game within 3 days
        if (await HasRecentReviewInGameAsync(authorId, postInfo.GameId))
        {
            throw new HttpException(HttpStatusCode.TooManyRequests,
                "Оценивать посты в одной игре можно раз в три дня");
        }

        var entity = new CreatePostReviewEntity
        {
            PostReviewId = _guidFactory.Create(),
            AuthorId = authorId,
            PostId = postId,
            PostAuthorId = postInfo.AuthorId,
            GameId = postInfo.GameId,
            CreatedUtc = _dateTimeProvider.Now,
            Sign = createReview.Sign,
            // Review bodies render on the Comment surface where [mod] is a green
            // mod block; strip it when authored by a non-moderator.
            Text = ModBlockSanitizer.SanitizeForAuthor(createReview.Text, author.Role)
        };

        try
        {
            // The post author's QualityRating moves with the row, in the one call
            // that writes both. Neutral is zero, so the sign is the delta.
            return await _repository.CreateAsync(entity, (int)createReview.Sign);
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedPost);
        }
    }

    /// <inheritdoc />
    public async Task<PostReview> GetAsync(Guid postId, Guid reviewId)
    {
        // Through the post, under the same room scope the create path reads it
        // with. By review identifier alone this answered on posts the caller
        // cannot open — a private room, a game they are not in — and handed out
        // the review body, the post author and the game along with it.
        var postInfo = await _repository.GetPostInfoAsync(postId, _identityProvider.Current.User.UserId);
        if (postInfo == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        var review = await GetByIdAsync(reviewId);

        // The route names both. A review of a different post reached through
        // this post's address is not this post's review, and answering with it
        // made the post identifier decoration.
        if (review.PostId != postId)
        {
            throw new HttpException(HttpStatusCode.NotFound, ReviewNotFound);
        }

        return review;
    }

    /// <summary>
    /// The review itself, with no scope of its own.
    /// </summary>
    /// <remarks>
    /// For the two paths that carry their own gate: editing asks for the author
    /// and their window, removal asks for the author or a senior moderator, and
    /// neither is reached from a route that names the post.
    /// </remarks>
    private async Task<PostReview> GetByIdAsync(Guid id)
    {
        var review = await _repository.GetAsync(id);
        if (review == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, ReviewNotFound);
        }

        return review;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetListAsync(
        Guid postId, PagingQuery query)
    {
        var totalCount = await _repository.CountAsync(postId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAsync(postId, pagingData);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetAllAsync(
        PagingQuery query, PostReviewFilter? filter = null)
    {
        var totalCount = await _repository.CountAllAsync(filter);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.Paging.EntitiesPerPage,
            totalCount);

        var reviews = await _repository.GetAllAsync(pagingData, filter);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public Task<PostReview?> GetByAuthorAsync(Guid postId, Guid authorId) =>
        _repository.GetByAuthorAsync(postId, authorId);

    /// <inheritdoc />
    public async Task<PostReview> UpdateAsync(UpdatePostReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await GetByIdAsync(updateReview.ReviewId);

        _intentionManager.ThrowIfForbidden(PostReviewIntention.Edit, review);

        // Check the author's edit window (admins can edit anytime)
        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role != UserRole.Admin && !CanEdit(review))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                $"Оценку можно править в течение {EditWindow.TotalMinutes:0} минут после публикации");
        }

        // Handle sign change impact on QualityRating
        var oldSign = review.Sign;
        var newSign = updateReview.Sign ?? oldSign;

        // The same probation the create path applies. Without it a newbie
        // published the neutral review the form allows them and then moved it
        // to a plus or a minus with an edit, which is the whole rule undone.
        if (newSign != oldSign && newSign != ReviewSign.Neutral &&
            await IsNewbieAsync(currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                RefusalMessage.SignedReviewNeedsExperience(ProbationPolicy.NewbiePostThreshold));
        }

        var entity = new UpdatePostReviewEntity(
            review.Id,
            Sign: updateReview.Sign,
            // Same treatment the text gets on the way in: the body renders on
            // the Comment surface, where [mod] is a green mod block.
            Text: updateReview.Text == null
                ? null
                : ModBlockSanitizer.SanitizeForAuthor(updateReview.Text, currentUser.Role),
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        // Both signs at once, in the call that writes the row. Neutral is zero,
        // so an edit that leaves the sign alone owes the counter nothing, and one
        // that moves it owes the difference — the old sign taken back and the new
        // one applied, which used to be two commits of their own.
        return await _repository.UpdateAsync(entity, (int)newSign - (int)oldSign);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var review = await GetByIdAsync(id);
        _intentionManager.ThrowIfForbidden(PostReviewIntention.Delete, review);

        // Who removed it and when: a senior moderator may remove somebody
        // else's review, and a removal with no hand behind it cannot be
        // reviewed afterwards.
        var entity = new UpdatePostReviewEntity(
            id,
            IsRemoved: true,
            DeletedUtc: _dateTimeProvider.Now,
            DeletedByUserId: _identityProvider.Current.User.UserId);

        // The post author's QualityRating gives back what the review gave it, in
        // the same write as the removal. Neutral is zero and owes nothing.
        await _repository.UpdateAsync(entity, -(int)review.Sign);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid authorId, Guid postId) =>
        _repository.ExistsAsync(authorId, postId);

    /// <inheritdoc />
    public Task<bool> HasRecentReviewInGameAsync(Guid authorId, Guid gameId)
    {
        var cutoffDate = _dateTimeProvider.Now - CooldownPerGame;
        return _repository.HasRecentReviewInGameAsync(authorId, gameId, cutoffDate);
    }

    /// <inheritdoc />
    public bool CanEdit(PostReview review)
    {
        var now = _dateTimeProvider.Now;
        var editDeadline = review.CreatedUtc + EditWindow;
        return now <= editDeadline;
    }

    private async Task<bool> IsNewbieAsync(Guid userId) =>
        ProbationPolicy.IsNewbie(await _repository.GetUserPostCountAsync(userId));
}
