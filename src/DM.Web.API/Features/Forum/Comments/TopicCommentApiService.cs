using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Comments;
using DM.Web.API.Shared.Comments;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Forum.Comments;

/// <inheritdoc />
internal class TopicCommentApiService : ITopicCommentApiService
{
    private readonly ITopicCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public TopicCommentApiService(
        ITopicCommentService commentService,
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
    public async Task<(IEnumerable<Comment> Comments, PagingInfo Paging)> Get(Guid topicId, CommentsQuery query)
    {
        var excludeUserIds = await CommentReading.HiddenAuthorsAsync(
            _blacklistChecker, _identityProvider.Current);
        var (comments, paging) = await _commentService.GetAsync(topicId, query, excludeUserIds);
        return (comments.Select(_mapper.Map<Comment>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid topicId, CreateCommentRequest request)
    {
        var createComment = _mapper.Map<CreateComment>(request);
        createComment.EntityId = topicId;
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
}
