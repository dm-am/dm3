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
    private readonly IBbTagNameCase _tagNames;

    public PostReviewService(
        IValidator<CreatePostReview> createValidator,
        IValidator<UpdatePostReview> updateValidator,
        IIntentionManager intentionManager,
        IPostReviewRepository repository,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IBbTagNameCase tagNames)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _tagNames = tagNames;
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

        // ReviewableAsync judges by the same method, so the form the page offers
        // and these refusals cannot part.
        var verdict = (await JudgeAsync(authorId,
            [new ReviewCandidate(postId, postInfo.AuthorId, postInfo.GameId)]))[postId];

        // Can't review own post
        if (verdict == Verdict.OwnPost)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нельзя оценивать собственный пост");
        }

        // Newbies can only create neutral post reviews. A question of the sign
        // and not of the post: the form offers a newbie "=" alone.
        if (createReview.Sign != ReviewSign.Neutral && await IsNewbieAsync(authorId))
        {
            throw new HttpException(HttpStatusCode.Forbidden,
                RefusalMessage.SignedReviewNeedsExperience(ProbationPolicy.NewbiePostThreshold));
        }

        // Check if already reviewed this post
        if (verdict == Verdict.AlreadyReviewed)
        {
            throw new HttpException(HttpStatusCode.Conflict, RefusalMessage.AlreadyReviewedPost);
        }

        // Check cooldown: can't review posts in the same game within 3 days.
        // A conflict with the state of things, not a rate limit: the page that
        // offered the form did not know of a review written since, and the
        // reader is told this rule in its own words, not "too many attempts".
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
            // Review bodies render on the Comment surface where [mod] is a green
            // mod block; strip it when authored by a non-moderator.
            Text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(createReview.Text, author.Role), BbTagSet.WithModBlock)
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
        // Through the post, under the same room scope GetAsync reads it with: a
        // post the reader cannot open has no reviews to list, and answers as a
        // post that is not there.
        if (await _repository.GetPostInfoAsync(postId, _identityProvider.Current.User.UserId) == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

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
                : _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(updateReview.Text, currentUser.Role), BbTagSet.WithModBlock),
            ModifiedUtc: _dateTimeProvider.Now,
            ModifiedByUserId: currentUser.UserId);

        // What the post author's counter owes is worked out by the write, from the
        // row it locks: the sign this edit asks for is the whole of what the
        // service knows, and a difference computed here would be a difference from
        // the review as it was read, which another change may already have moved.
        return await _repository.UpdateAsync(entity);
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
        // the same write as the removal and worked out there: a review already
        // taken down by somebody else gives nothing back a second time.
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
    /// What stands between the rater and a review of each post: his own post,
    /// a review of his already on it, a review of his in the same game during
    /// the last three days. In that order, the order CreateAsync tells them in.
    /// </summary>
    /// <remarks>
    /// One method for the write and for the flags on a page of posts, two
    /// queries for the whole page. Sequential on purpose: both read the one
    /// DmDbContext of the scope, and a context refuses parallel operations.
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
    public bool CanEdit(PostReview review)
    {
        var now = _dateTimeProvider.Now;
        var editDeadline = review.CreatedUtc + EditWindow;
        return now <= editDeadline;
    }

    private async Task<bool> IsNewbieAsync(Guid userId) =>
        ProbationPolicy.IsNewbie(await _repository.GetUserPostCountAsync(userId));
}
