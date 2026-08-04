using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Web.API.Shared.Comments;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using DiscussionResponse = DM.Web.API.Shared.Dto.DiscussionResponse;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Blog.PublicationComments;

/// <inheritdoc />
internal class PublicationCommentApiService : IPublicationCommentApiService
{
    private readonly IPublicationCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PublicationCommentApiService(
        IPublicationCommentService commentService,
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
    public async Task<ListEnvelope<Comment>> Get(Guid publicationId, PublicationCommentsQuery query)
    {
        var excludeUserIds = await CommentReading.HiddenAuthorsAsync(
            _blacklistChecker, _identityProvider.Current);
        var (comments, paging) = await _commentService.GetAsync(publicationId, query, excludeUserIds);
        return new ListEnvelope<Comment>(comments.Select(_mapper.Map<Comment>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid publicationId, CreateCommentRequest request)
    {
        var createComment = _mapper.Map<CreateComment>(request);
        createComment.EntityId = publicationId;
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
    public async Task<Envelope<Comment>> Update(Guid commentId, UpdateCommentRequest request)
    {
        var updateComment = _mapper.Map<UpdateComment>(request);
        updateComment.CommentId = commentId;
        var updatedComment = await _commentService.UpdateAsync(updateComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(updatedComment));
    }

    /// <inheritdoc />
    public Task Delete(Guid commentId) => _commentService.DeleteAsync(commentId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid publicationId) => _commentService.MarkAsReadAsync(publicationId);
}
