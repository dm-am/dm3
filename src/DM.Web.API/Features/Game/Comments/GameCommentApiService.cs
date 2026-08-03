using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Game.Features.Comments;
using DM.Web.API.Shared.Comments;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using DiscussionResponse = DM.Web.API.Shared.Dto.DiscussionResponse;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Game.Comments;

/// <inheritdoc />
internal class GameCommentApiService : IGameCommentApiService
{
    private readonly IGameCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameCommentApiService(
        IGameCommentService commentService,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker blacklistChecker,
        IMapper mapper)
    {
        _commentService = commentService;
        _identityProvider = identityProvider;
        _blacklistChecker = blacklistChecker;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<DiscussionResponse> GetDiscussion(Guid gameId, GameCommentsQuery query)
    {
        var identity = _identityProvider.Current;
        var excludeUserIds = await CommentReading.HiddenAuthorsAsync(_blacklistChecker, identity);
        var (comments, paging) = await _commentService.GetAsync(gameId, query, excludeUserIds);

        return CommentReading.ToDiscussion(comments, paging, identity, _mapper);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Comment>> Get(Guid gameId, GameCommentsQuery query)
    {
        var excludeUserIds = await CommentReading.HiddenAuthorsAsync(
            _blacklistChecker, _identityProvider.Current);
        var (comments, paging) = await _commentService.GetAsync(gameId, query, excludeUserIds);
        return new ListEnvelope<Comment>(comments.Select(_mapper.Map<Comment>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid gameId, CreateCommentRequest request)
    {
        var createComment = _mapper.Map<CreateComment>(request);
        createComment.EntityId = gameId;
        var createdComment = await _commentService.CreateAsync(createComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(createdComment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Get(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        return new Envelope<Comment>(_mapper.Map<Comment>(comment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Update(Guid commentId, Comment comment)
    {
        var updateComment = _mapper.Map<UpdateComment>(comment);
        updateComment.CommentId = commentId;
        var updatedComment = await _commentService.UpdateAsync(updateComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(updatedComment));
    }

    /// <inheritdoc />
    public Task Delete(Guid commentId) => _commentService.DeleteAsync(commentId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid gameId) => _commentService.MarkAsReadAsync(gameId);
}
