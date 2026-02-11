using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Community.BusinessProcesses.Blogs;
using Comment = DM.Services.Common.Dto.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Reading;

/// <inheritdoc />
internal class CommentReadingService : ICommentReadingService
{
    private readonly IBlogService _blogService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly ICommentReadingRepository _commentRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public CommentReadingService(
        IBlogService blogService,
        IIdentityProvider identityProvider,
        IUnreadCountersRepository unreadCountersRepository,
        ICommentReadingRepository commentRepository)
    {
        _blogService = blogService;
        _unreadCountersRepository = unreadCountersRepository;
        _commentRepository = commentRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> comments, PagingResult paging)> Get(
        Guid publicationId, PagingQuery query)
    {
        await _blogService.GetPublication(publicationId);

        var totalCount = await _commentRepository.Count(publicationId);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _commentRepository.Get(publicationId, paging);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> Get(Guid commentId)
    {
        return await _commentRepository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.Gone, $"Comment {commentId} not found");
    }

    /// <inheritdoc />
    public async Task MarkAsRead(Guid publicationId)
    {
        await _blogService.GetPublication(publicationId);
        await _unreadCountersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, publicationId);
    }

    /// <inheritdoc />
    public async Task MarkBlogCommentsAsRead(Guid blogId)
    {
        var blog = await _blogService.GetBlog(blogId);
        await _unreadCountersRepository.FlushAll(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, blog.Id);
    }
}
