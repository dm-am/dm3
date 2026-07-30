using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IEventProducer _invokedEventProducer;

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
        IEventProducer invokedEventProducer)
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
        _invokedEventProducer = invokedEventProducer;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var game = await _gameService.GetAsync(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(GameIntention.CreateComment, game);

        // Check blacklist
        var currentUser = _identityProvider.Current.User;
        if (game.BlacklistedUsers.Any(b => b.UserId == currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }

        var entity = new CreateGameCommentEntity
        {
            CommentId = _guidFactory.Create(),
            GameId = game.Id,
            AuthorId = currentUser.UserId,
            // Strip [mod] authored by a non-moderator (it renders as a green
            // mod block on the Comment surface); Moderator+ may author it.
            Text = ModBlockSanitizer.SanitizeForAuthor(createComment.Text.Trim(), currentUser.Role),
            NewCommentCount = game.CommentCount + 1,
            CreatedUtc = _dateTimeProvider.Now
        };

        var createdComment = await _repository.Create(entity);

        await _countersRepository.IncrementAsync(game.Id, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.NewGameComment, entity.CommentId);

        return createdComment;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid gameId, GameCommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var game = await _gameService.GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.ReadComments, game);

        var totalCount = await _repository.Count(gameId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(gameId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        return await _repository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.Gone, RefusalMessage.CommentNotFound(commentId));
    }

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await GetAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        var text = updateComment.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            // Strip [mod] authored by a non-moderator before comparing/saving.
            text = ModBlockSanitizer.SanitizeForAuthor(text, _identityProvider.Current.User.Role);
        }
        if (string.IsNullOrWhiteSpace(text) || text == comment.Text)
        {
            return comment;
        }

        var entity = new UpdateGameCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            ModifiedUtc = _dateTimeProvider.Now
        };

        var updatedComment = await _repository.Update(entity);
        await _invokedEventProducer.SendAsync(EventType.ChangedGameComment, updateComment.CommentId);
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

        Guid? newLastCommentId = null;
        if (comment.IsLastComment)
        {
            newLastCommentId = await _repository.GetSecondLastCommentId(comment.GameId);
        }

        var entity = new DeleteGameCommentEntity
        {
            CommentId = commentId,
            GameId = comment.GameId,
            NewCommentCount = Math.Max(0, comment.GameCommentCount - 1),
            NewLastCommentId = newLastCommentId
        };

        await _repository.Delete(entity);
        await _countersRepository.DecrementAsync(comment.GameId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.SendAsync(EventType.DeletedGameComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.ReadComments, game);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, gameId);
    }
}
