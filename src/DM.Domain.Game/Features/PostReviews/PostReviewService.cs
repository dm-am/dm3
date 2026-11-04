using System;
using System.Collections.Generic;
using System.Linq;
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
using FluentValidation;

namespace DM.Domain.Game.Features.PostReviews;

/// <inheritdoc />
internal class PostReviewService : IPostReviewService
{
    /// <summary>
    /// For a typo, not a change of mind; deleting has no window.
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

    public PostReviewService(
        IValidator<CreatePostReview> createValidator,
        IValidator<UpdatePostReview> updateValidator,
        IIntentionManager intentionManager,
        IPostReviewRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<PostReview> CreateAsync(CreatePostReview createReview)
    {
        await _createValidator.ValidateAndThrowAsync(createReview);
        _intentionManager.ThrowIfForbidden(PostReviewIntention.Create);

        var author = _identityProvider.Current.User;
        var authorId = author.UserId;
        var postId = createReview.PostId;

        var postInfo = await _repository.GetPostInfoAsync(postId, authorId);
        if (postInfo == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        // ReviewableAsync judges by the same method, so the form and these refusals cannot part.
        var verdict = (await JudgeAsync(authorId,
            [new ReviewCandidate(postId, postInfo.AuthorId, postInfo.GameId)]))[postId];

        if (verdict == Verdict.OwnPost)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя оценивать собственный пост");
        }

        // A question of the sign, not of the post: the form offers a newbie "=" alone.
        if (createReview.Sign != ReviewSign.Neutral && await IsNewbieAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                RefusalMessage.SignedReviewNeedsExperience(ProbationPolicy.NewbiePostThreshold));
        }

        if (verdict == Verdict.AlreadyReviewed)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedPost);
        }

        // A conflict with the state of things, not a rate limit.
        if (verdict == Verdict.InCooldown)
        {
            throw new HttpException(HttpStatusCode.Conflict,
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
            Text = ModBlockSanitizer.SanitizeForAuthor(createReview.Text, author.Role)
        };

        try
        {
            return await _repository.CreateAsync(entity);
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedPost);
        }
    }

    /// <inheritdoc />
    public async Task<PostReview> GetAsync(Guid postId, Guid reviewId)
    {
        // Through the post, under the room scope the create path reads it with.
        var postInfo = await _repository.GetPostInfoAsync(postId, _identityProvider.Current.User.UserId);
        if (postInfo == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        var review = await GetByIdAsync(reviewId);

        if (review.PostId != postId)
        {
            throw new HttpException(HttpStatusCode.NotFound, ReviewNotFound);
        }

        return review;
    }

    /// <summary>
    /// No scope of its own: editing and removal carry their own gates.
    /// </summary>
    private async Task<PostReview> GetByIdAsync(Guid id) =>
        await _repository.GetAsync(id) ?? throw new HttpException(HttpStatusCode.NotFound, ReviewNotFound);

    /// <inheritdoc />
    public async Task<(IEnumerable<PostReview> Reviews, PagingResult Paging)> GetListAsync(
        Guid postId, PagingQuery query)
    {
        // Through the post, under the room scope GetAsync reads it with.
        if (await _repository.GetPostInfoAsync(postId, _identityProvider.Current.User.UserId) == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        var totalCount = await _repository.CountAsync(postId);
        var pagingData = new PagingData(
            query,
            _identityProvider.Current.Settings.PageSize,
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
            _identityProvider.Current.Settings.PageSize,
            totalCount);

        var reviews = await _repository.GetAllAsync(pagingData, filter);
        return (reviews, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<PostReview> UpdateAsync(UpdatePostReview updateReview)
    {
        await _updateValidator.ValidateAndThrowAsync(updateReview);
        var review = await GetByIdAsync(updateReview.ReviewId);

        _intentionManager.ThrowIfForbidden(PostReviewIntention.Edit, review);

        var currentUser = _identityProvider.Current.User;
        if (currentUser.Role != UserRole.Admin && !CanEdit(review))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                $"Оценку можно править в течение {EditWindow.TotalMinutes:0} минут после отправки");
        }

        var oldSign = review.Sign;
        var newSign = updateReview.Sign ?? oldSign;

        // The probation of the create path, or an edit would move a neutral review to a sign.
        if (newSign != oldSign && newSign != ReviewSign.Neutral &&
            await IsNewbieAsync(currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                RefusalMessage.SignedReviewNeedsExperience(ProbationPolicy.NewbiePostThreshold));
        }

        var entity = new UpdatePostReviewEntity(
            review.Id,
            Sign: updateReview.Sign,
            Text: ModBlockSanitizer.SanitizeForAuthor(updateReview.Text, currentUser.Role),
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        // The rating difference is worked out by the write, from the row it locks.
        return await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var review = await GetByIdAsync(id);
        _intentionManager.ThrowIfForbidden(PostReviewIntention.Delete, review);

        // A senior moderator may remove somebody else's review, so the hand is recorded.
        var entity = new UpdatePostReviewEntity(
            id,
            IsRemoved: true,
            DeletedUtc: _dateTimeProvider.Now,
            DeletedByUserId: _identityProvider.Current.User.UserId);

        // The write gives the rating back, and nothing a second time for a review already removed.
        await _repository.UpdateAsync(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> ReviewableAsync(IReadOnlyCollection<ReviewCandidate> posts)
    {
        if (posts.Count == 0 || !_intentionManager.IsAllowed(PostReviewIntention.Create))
        {
            return new HashSet<Guid>();
        }

        var verdicts = await JudgeAsync(_identityProvider.Current.User.UserId, posts);
        return verdicts.Where(v => v.Value == Verdict.Allowed).Select(v => v.Key).ToHashSet();
    }

    /// <summary>
    /// In the order CreateAsync tells the refusals.
    /// </summary>
    /// <remarks>
    /// Sequential: both queries read the one DmDbContext of the scope, which refuses parallel operations.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Guid, Verdict>> JudgeAsync(
        Guid raterId, IReadOnlyCollection<ReviewCandidate> posts)
    {
        var others = posts.Where(p => p.AuthorId != raterId).ToArray();
        IReadOnlyCollection<Guid> reviewed = [];
        IReadOnlyCollection<Guid> cooling = [];
        if (others.Length > 0)
        {
            reviewed = await _repository.SelectReviewedPostIdsAsync(
                raterId, others.Select(p => p.PostId).Distinct().ToArray());
            cooling = await _repository.SelectGamesReviewedSinceAsync(
                raterId, others.Select(p => p.GameId).Distinct().ToArray(), _dateTimeProvider.Now - CooldownPerGame);
        }

        var verdicts = new Dictionary<Guid, Verdict>();
        foreach (var post in posts)
        {
            verdicts[post.PostId] =
                post.AuthorId == raterId ? Verdict.OwnPost
                : reviewed.Contains(post.PostId) ? Verdict.AlreadyReviewed
                : cooling.Contains(post.GameId) ? Verdict.InCooldown
                : Verdict.Allowed;
        }

        return verdicts;
    }

    /// <summary>What <see cref="JudgeAsync" /> found for one post.</summary>
    private enum Verdict
    {
        Allowed,
        OwnPost,
        AlreadyReviewed,
        InCooldown
    }

    /// <inheritdoc />
    public bool CanEdit(PostReview review) => _dateTimeProvider.Now <= review.CreatedUtc + EditWindow;

    private async Task<bool> IsNewbieAsync(Guid userId) =>
        ProbationPolicy.IsNewbie(await _repository.GetUserPostCountAsync(userId));
}
