using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Game.Features.Comments;
using DM.Web.API.Shared.Comments;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Game.Comments;

/// <inheritdoc />
internal class GameCommentApiService : IGameCommentApiService
{
    private readonly IGameCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly CommentMapper _mapper;
    private readonly IQuoteSourceService _quoteSourceService;

    /// <inheritdoc />
    public GameCommentApiService(
        IGameCommentService commentService,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker blacklistChecker,
        CommentMapper mapper,
        IQuoteSourceService quoteSourceService)
    {
        _commentService = commentService;
        _identityProvider = identityProvider;
        _blacklistChecker = blacklistChecker;
        _mapper = mapper;
        _quoteSourceService = quoteSourceService;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Comment>> Get(Guid gameId, CommentsQuery query)
    {
        var excludeUserIds = await CommentReading.HiddenAuthorsAsync(
            _blacklistChecker, _identityProvider.Current);
        var (comments, paging) = await _commentService.GetAsync(gameId, query, excludeUserIds);
        return new ListEnvelope<Comment>(comments.Select(_mapper.ToComment), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid gameId, CreateCommentRequest request)
    {
        var createComment = _mapper.ToCreateComment(request);
        createComment.EntityId = gameId;
        var createdComment = await _commentService.CreateAsync(createComment);
        return new Envelope<Comment>(_mapper.ToComment(createdComment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Get(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        return new Envelope<Comment>(_mapper.ToComment(comment));
    }

    /// <inheritdoc />
    public async Task<Envelope<QuoteSource>> GetQuote(Guid commentId)
    {
        // Read through the same service the ordinary read goes through: a
        // comment this reader may not have is refused by that read, and there is
        // no second permission rule here to keep in step with the first.
        var comment = await _commentService.GetAsync(commentId);
        return CommentReading.ToQuote(comment, _mapper, _quoteSourceService);
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Update(Guid commentId, UpdateCommentRequest request)
    {
        var updateComment = _mapper.ToUpdateComment(request);
        updateComment.CommentId = commentId;
        var updatedComment = await _commentService.UpdateAsync(updateComment);
        return new Envelope<Comment>(_mapper.ToComment(updatedComment));
    }

    /// <inheritdoc />
    public Task Delete(Guid commentId) => _commentService.DeleteAsync(commentId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid gameId) => _commentService.MarkAsReadAsync(gameId);
}
