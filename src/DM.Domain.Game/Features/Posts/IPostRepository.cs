using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

public interface IPostRepository
{
    Task<int> Count(Guid roomId, RoomPostsQuery query, Guid userId);

    /// <summary>
    /// Each post carries its <see cref="Post.Number"/> in the room
    /// </summary>
    Task<IEnumerable<Post>> Get(Guid roomId, RoomPostsQuery query, PagingData paging, Guid userId);

    /// <summary>
    /// Carries its <see cref="Post.Number"/> in the room
    /// </summary>
    Task<Post?> Get(Guid postId, Guid userId, CancellationToken ct = default);

    /// <remarks>
    /// <paramref name="viewerId"/> does not widen or narrow the feed, which admits only open rooms of approved
    /// games; it decides whether each game reports the reader as a subscriber. <see cref="Guid.Empty" /> for a guest.
    /// </remarks>
    Task<(IEnumerable<Post> Posts, int TotalCount)> GetRated(PostsQuery query, Guid viewerId);

    Task<Post> Create(CreatePostEntity createPost);

    Task<Post?> Update(UpdatePostEntity updatePost);

    /// <remarks>
    /// The author's post count and the attachments move with the mark.
    /// </remarks>
    /// <returns>Whether this call removed the post, so a caller does not repeat its own counters and events</returns>
    Task<bool> Delete(Guid postId, Guid deletedByUserId);
}
