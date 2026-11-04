using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Blog.Features.Comments;
using DM.Web.API.Shared.Comments;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CommentRequest = DM.Web.API.Shared.Dto.CommentRequest;

namespace DM.Web.API.Features.Blog.Comments;

public class BlogCommentApiService
{
    private readonly IBlogCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly CommentMapper _mapper;

    public BlogCommentApiService(
        IBlogCommentService commentService,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker blacklistChecker,
        CommentMapper mapper)
    {
        _commentService = commentService;
        _identityProvider = identityProvider;
        _blacklistChecker = blacklistChecker;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<Comment>> Get(Guid blogId, CommentsQuery query)
    {
        var excludeUserIds = await CommentReading.HiddenAuthorsAsync(
            _blacklistChecker, _identityProvider.Current);
        var (comments, paging) = await _commentService.GetAsync(blogId, query, excludeUserIds);
        return new ListEnvelope<Comment>(comments.Select(_mapper.ToComment), new PagingInfo(paging));
    }

    public async Task<Envelope<Comment>> Create(Guid blogId, CommentRequest request)
    {
        var createComment = _mapper.ToCreateComment(request);
        createComment.EntityId = blogId;
        var createdComment = await _commentService.CreateAsync(createComment);
        return new Envelope<Comment>(_mapper.ToComment(createdComment));
    }

    public async Task<Envelope<Comment>> Get(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        return new Envelope<Comment>(_mapper.ToComment(comment));
    }

    public async Task<Envelope<Comment>> Update(Guid commentId, CommentRequest request)
    {
        var updateComment = _mapper.ToUpdateComment(request);
        updateComment.CommentId = commentId;
        var updatedComment = await _commentService.UpdateAsync(updateComment);
        return new Envelope<Comment>(_mapper.ToComment(updatedComment));
    }

    public Task Delete(Guid commentId) => _commentService.DeleteAsync(commentId);

    public Task MarkAsRead(Guid blogId) => _commentService.MarkAsReadAsync(blogId);
}
