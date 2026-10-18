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
    private readonly IEventProducer _invokedEventProducer;
    private readonly IBbTagNameCase _tagNames;

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
        IEventProducer invokedEventProducer,
        IBbTagNameCase tagNames)
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
        _tagNames = tagNames;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var game = await _gameService.GetAsync(createComment.EntityId);
        var currentUser = _identityProvider.Current.User;

        // The blacklist closes writing: reading this game is open to the user it
        // blacklisted, commenting in it is not. The intention carries the same
        // rule; asked first here, it answers with the reason instead of the
        // generic refusal of the gate below. The gate is MayCreate's question,
        // so the form the page offers and this refusal cannot part.
        game.ThrowIfBlacklisted(currentUser.UserId);
        _intentionManager.ThrowIfForbidden(GameIntention.CreateComment, game);

        var entity = new CreateGameCommentEntity
        {
            CommentId = _guidFactory.Create(),
            GameId = game.Id,
            AuthorId = currentUser.UserId,
            // Strip [mod] authored by a non-moderator (it renders as a green
            // mod block on the Comment surface); Moderator+ may author it.
            Text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(createComment.Text.Trim(), currentUser.Role), BbTagSet.WithModBlock),
            CreatedUtc = _dateTimeProvider.Now
        };

        var createdComment = await _repository.Create(entity);

        await _countersRepository.IncrementAsync(game.Id, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.NewGameComment, entity.CommentId);

        return createdComment;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The intention CreateAsync is refused by, and nothing beside it: the
    /// blacklist CreateAsync asks first is one of the intention's own arms,
    /// asked there for the words only. The mode of the discussion, the roles
    /// and the ban are all in it (GameIntentionResolver.CreateComment).
    /// </remarks>
    public bool MayCreate(Games.Game game) =>
        _intentionManager.IsAllowed(GameIntention.CreateComment, game);

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid gameId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        // The discussion is read by whoever may read the game, and the read of
        // the game below is that whole question: no mode, list or ban narrows it.
        await _gameService.GetAsync(gameId);

        var totalCount = await _repository.Count(gameId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(gameId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        var comment = await FindAsync(commentId);

        // The four discussions share one table and the row above is read by its
        // id alone, so the discussion is asked here exactly as the listing asks it.
        // A discussion that is not there to the reader - hidden, removed, or of
        // another kind, which this route has nothing to check by - answers exactly
        // as a comment that is not there: a different answer would tell a
        // stranger that the id resolves, and to what.
        await DiscussionRefusal.AsCommentNotFound(commentId,
            () => _gameService.GetAsync(comment.EntityId));

        return comment;
    }

    /// <summary>
    /// The comment by its identifier, among the comments of games only. The edit
    /// keeps to this lookup: who may edit is CommentIntention.Edit's answer on
    /// the row, and reading the discussion is not one of its conditions.
    /// </summary>
    /// <remarks>
    /// Narrowed to games by the repository. The four discussions share one table,
    /// and a lookup by id alone let this route edit a comment of a blog or a
    /// topic: the rules of that other discussion, its blacklist among them, were
    /// never asked, so a user blacklisted in a blog rewrote his comment there
    /// through this address.
    /// </remarks>
    private async Task<Comment> FindAsync(Guid commentId) =>
        await _repository.Get(commentId) ??
        throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await FindAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        // Rewriting a comment publishes text exactly as writing one does, so the
        // blacklist and the ban are asked here too, on the terms
        // GameIntention.CreateComment sets: a game the user leads or was
        // accepted into stays open to the ban. Only the author is asked, a
        // moderator editing somebody else's comment is moderating and neither
        // list takes a moderator tool away.
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

        var text = updateComment.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            // Strip [mod] authored by a non-moderator before comparing/saving.
            text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(text, currentUser.Role), BbTagSet.WithModBlock);
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

        var entity = new DeleteGameCommentEntity
        {
            CommentId = commentId,
            GameId = comment.GameId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now
        };

        // The game's comment count and its last-comment pointer move inside
        // Delete, in the transaction that marks the comment.
        var removed = await _repository.Delete(entity);
        if (!removed)
        {
            // Somebody else removed this comment between the read above and the
            // write. The game's unread counter and the event belong to the removal
            // that happened, and repeating them here takes the counter down twice
            // for one comment. The caller is answered the same either way: the
            // comment is gone, which is what they asked for.
            return;
        }

        await _countersRepository.DecrementAsync(comment.GameId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.SendAsync(EventType.DeletedGameComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid gameId)
    {
        await _gameService.GetAsync(gameId);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, gameId);
    }
}
