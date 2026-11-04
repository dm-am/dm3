using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

public interface IPostService
{
    Task<Post> CreateAsync(CreatePost createPost);

    Task<(IEnumerable<Post> posts, PagingResult paging)> GetAllAsync(Guid roomId, RoomPostsQuery query);

    Task<Post> GetAsync(Guid postId, CancellationToken ct = default);

    Task MarkAsReadAsync(Guid roomId);

    Task<(IEnumerable<Post> Posts, PagingResult Paging)> GetRatedAsync(PostsQuery query);

    Task<Post> UpdateAsync(UpdatePost updatePost);

    /// <summary>
    /// The posts of <paramref name="posts"/> whose text the current user may edit, by the check
    /// <see cref="UpdateAsync"/> refuses the edit with: the author, and moderation and the leads only
    /// over a post whose hidden blocks they all read.
    /// </summary>
    IReadOnlySet<Guid> Editable(IEnumerable<Post> posts);

    /// <summary>
    /// Soft delete
    /// </summary>
    Task DeleteAsync(Guid postId);
}
