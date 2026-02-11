using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Forum.BusinessProcesses.Commentaries.Creating;
using DM.Services.Forum.BusinessProcesses.Commentaries.Deleting;
using DM.Services.Forum.BusinessProcesses.Commentaries.Reading;
using DM.Services.Forum.BusinessProcesses.Commentaries.Updating;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Shared;
using Comment = DM.Web.API.Dto.Shared.Comment;

namespace DM.Web.API.Services.Boards;

/// <inheritdoc />
internal class CommentApiService : ICommentApiService
{
    private readonly ICommentaryReadingService readingService;
    private readonly ICommentaryCreatingService creatingService;
    private readonly ICommentaryUpdatingService updatingService;
    private readonly ICommentaryDeletingService deletingService;
    private readonly IIdentityProvider identityProvider;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public CommentApiService(
        ICommentaryReadingService readingService,
        ICommentaryCreatingService creatingService,
        ICommentaryUpdatingService updatingService,
        ICommentaryDeletingService deletingService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.creatingService = creatingService;
        this.updatingService = updatingService;
        this.deletingService = deletingService;
        this.identityProvider = identityProvider;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<DiscussionResponse> GetDiscussion(Guid topicId, PagingQuery query)
    {
        var (comments, paging) = await readingService.Get(topicId, query);
        var identity = identityProvider.Current;
        var currentUserId = identity.User?.UserId ?? Guid.Empty;
        var isAuthenticated = identity.User?.IsAuthenticated ?? false;
        var isModerator = (identity.User?.Role ?? UserRole.Guest) >= UserRole.Moderator;

        var discussionComments = comments.Select(c =>
        {
            var dc = mapper.Map<DiscussionComment>(c);
            var isAuthor = c.Author?.UserId == currentUserId;
            dc.IsLikedByMe = c.Likes?.Any(l => l.UserId == currentUserId) ?? false;
            dc.CanEdit = isAuthor || isModerator;
            dc.CanDelete = isAuthor || isModerator;
            dc.CanLike = isAuthenticated && !isAuthor;
            return dc;
        }).ToList();

        var totalLikes = discussionComments.Sum(c => c.LikesCount);

        return new DiscussionResponse(
            discussionComments,
            new Paging(paging),
            totalLikes,
            isAuthenticated);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Comment>> Get(Guid topicId, PagingQuery query)
    {
        var (comments, paging) = await readingService.Get(topicId, query);
        return new ListEnvelope<Comment>(comments.Select(mapper.Map<Comment>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid topicId, CreateCommentRequest request)
    {
        var createComment = mapper.Map<CreateComment>(request);
        createComment.EntityId = topicId;
        var createdComment = await creatingService.Create(createComment);
        return new Envelope<Comment>(mapper.Map<Comment>(createdComment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Get(Guid commentId)
    {
        var comment = await readingService.Get(commentId);
        return new Envelope<Comment>(mapper.Map<Comment>(comment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Update(Guid commentId, Comment comment)
    {
        var updateComment = mapper.Map<UpdateComment>(comment);
        updateComment.CommentId = commentId;
        var updatedComment = await updatingService.Update(updateComment);
        return new Envelope<Comment>(mapper.Map<Comment>(updatedComment));
    }

    /// <inheritdoc />
    public Task Delete(Guid commentId) => deletingService.Delete(commentId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid topicId) => readingService.MarkAsRead(topicId);

    /// <inheritdoc />
    public Task MarkAsRead(string forumId) => readingService.MarkAsRead(forumId);

    /// <inheritdoc />
    public Task MarkAllAsRead() => readingService.MarkAllAsRead();
}