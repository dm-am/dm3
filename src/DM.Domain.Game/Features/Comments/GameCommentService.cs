using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Events;
using DM.Domain.Core.UnreadCounters;
using FluentValidation;

namespace DM.Domain.Game.Features.Comments;

/// <inheritdoc />
internal class GameCommentService : IGameCommentService
{
    private readonly IValidator<CreateComment> _createValidator;
    private readonly IValidator<UpdateComment> _updateValidator;
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IGameCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _producer;

    public GameCommentService(
        IValidator<CreateComment> createValidator,
        IValidator<UpdateComment> updateValidator,
        IGameService gameService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IGameCommentRepository repository,
        IUnreadCountersRepository countersRepository,
        IEventProducer producer)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _gameService = gameService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _repository = repository;
        _countersRepository = countersRepository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var game = await _gameService.GetAsync(createComment.EntityId);
        var currentUser = _identityProvider.Current.User;

        // The intention carries the same rule; asked first, this one names the reason.
        game.ThrowIfBlacklisted(currentUser.UserId);
        _intentionManager.ThrowIfForbidden(GameIntention.CreateComment, game);

        var entity = new CreateGameCommentEntity
        {
            CommentId = _guidFactory.Create(),
            GameId = game.Id,
            AuthorId = currentUser.UserId,
            Text = ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role),
            CreatedUtc = _dateTimeProvider.Now
        };

        var createdComment = await _repository.Create(entity);

        await _countersRepository.IncrementAsync(game.Id, UnreadEntryType.Message);
        await _producer.SendAsync(EventType.NewGameComment, entity.CommentId);

        return createdComment;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The intention CreateAsync is refused by, so the form and the refusal cannot part.
    /// </remarks>
    public bool MayCreate(Games.Game game) =>
        _intentionManager.IsAllowed(GameIntention.CreateComment, game);

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid gameId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        // Whoever may read the game reads its discussion: no mode, list or ban narrows it.
        await _gameService.GetAsync(gameId);

        var totalCount = await _repository.Count(gameId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.PageSize, totalCount);

        var comments = await _repository.Get(gameId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        var comment = await FindAsync(commentId);

        // A discussion the reader may not open answers as a missing comment, so the id tells nothing.
        await DiscussionRefusal.AsCommentNotFound(commentId,
            () => _gameService.GetAsync(comment.EntityId));

        return comment;
    }

    /// <summary>
    /// The comment among the comments of games only: the four discussions share one table.
    /// </summary>
    private async Task<Comment> FindAsync(Guid commentId) =>
        await _repository.Get(commentId) ??
        throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await FindAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        // Rewriting publishes text as writing does, so the author is held to the blacklist and the ban;
        // a moderator editing somebody else's comment is moderating.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId)
        {
            var game = await DiscussionRefusal.AsCommentNotFound(updateComment.CommentId,
                () => _gameService.GetAsync(comment.EntityId));
            game.ThrowIfBlacklisted(currentUser.UserId);
            if (!currentUser.MaySpeak())
            {
                currentUser.ThrowIfMayNotComment(inOwnSpace: game.GetRoles(currentUser.UserId).IsOwnGame());
            }
        }

        var text = AuthoredText.AsTyped(updateComment.Text);
        if (!string.IsNullOrEmpty(text))
        {
            text = ModBlockSanitizer.SanitizeForAuthor(text, currentUser.Role);
        }
        if (string.IsNullOrWhiteSpace(text) || text == comment.Text)
        {
            return comment;
        }

        var entity = new UpdateGameCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            ModifiedUtc = _dateTimeProvider.Now,
            EditorUserId = currentUser.UserId
        };

        var updatedComment = await _repository.Update(entity);
        await _producer.SendAsync(EventType.ChangedGameComment, updateComment.CommentId);
        return updatedComment;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid commentId)
    {
        var comment = await _repository.GetForDelete(commentId);
        if (comment == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));
        }

        _intentionManager.ThrowIfForbidden(CommentIntention.Delete, comment);

        var entity = new DeleteGameCommentEntity
        {
            CommentId = commentId,
            GameId = comment.GameId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now
        };

        var removed = await _repository.Delete(entity);
        if (!removed)
        {
            // Removed by somebody else in between: the counter and the event belong to that removal.
            return;
        }

        await _countersRepository.DecrementAsync(comment.GameId, UnreadEntryType.Message, comment.CreatedUtc);

        await _producer.SendAsync(EventType.DeletedGameComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid gameId)
    {
        await _gameService.GetAsync(gameId);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, gameId);
    }
}
