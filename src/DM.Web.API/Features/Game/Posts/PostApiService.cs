using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.PostReviews;
using DM.Domain.Game.Features.Posts;
using DM.Web.API.Shared.Dto;
using DomainPost = DM.Domain.Game.Features.Posts.Post;

namespace DM.Web.API.Features.Game.Posts;

public class PostApiService
{
    private readonly IPostService _postService;
    private readonly IPostReviewService _reviewService;
    private readonly PostMapper _mapper;

    public PostApiService(
        IPostService postService,
        IPostReviewService reviewService,
        PostMapper mapper)
    {
        _postService = postService;
        _reviewService = reviewService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<Post>> Get(Guid roomId, RoomPostsQuery query)
    {
        var (posts, paging) = await _postService.GetAllAsync(roomId, query);
        return new ListEnvelope<Post>(await PostsForViewer(posts), new PagingInfo(paging));
    }

    public async Task<Envelope<Post>> Get(Guid postId)
    {
        var post = await _postService.GetAsync(postId);
        return new Envelope<Post>((await PostsForViewer([post])).Single());
    }

    public async Task<Envelope<Post>> Create(Guid roomId, CreatePostRequest post)
    {
        var createPost = _mapper.ToCreatePost(post);
        createPost.RoomId = roomId;
        var createdPost = await _postService.CreateAsync(createPost);
        return new Envelope<Post>((await PostsForViewer([createdPost])).Single());
    }

    public async Task<Envelope<Post>> Update(Guid postId, UpdatePostRequest request)
    {
        var updatePost = _mapper.ToUpdatePost(request);
        updatePost.PostId = postId;
        var updatedPost = await _postService.UpdateAsync(updatePost);
        return new Envelope<Post>((await PostsForViewer([updatedPost])).Single());
    }

    public Task Delete(Guid postId) => _postService.DeleteAsync(postId);

    public Task MarkAsRead(Guid roomId) => _postService.MarkAsReadAsync(roomId);

    public async Task<ListEnvelope<Post>> GetRated(PostsQuery query)
    {
        var (posts, paging) = await _postService.GetRatedAsync(query);
        return new ListEnvelope<Post>(await PostsForViewer(posts), new PagingInfo(paging));
    }

    /// <summary>
    /// Posts as the viewer gets them: each post, and what he may do with it.
    /// Every answer that carries posts comes through here, the lists included,
    /// because a post is only ever shown in a list or alone and the review form
    /// is offered under it in both. One question for the whole page, not one
    /// per post.
    /// </summary>
    private async Task<IReadOnlyList<Post>> PostsForViewer(IEnumerable<DomainPost> posts)
    {
        var page = posts.ToArray();
        var reviewable = await _reviewService.ReviewableAsync(
            page.Select(p => new ReviewCandidate(p.Id, p.AuthorUserId, p.GameId)).ToArray());
        var editable = _postService.Editable(page);
        return page.Select(p =>
        {
            var mapped = _mapper.ToPost(p);
            mapped.Viewer = new PostViewer
            {
                CanReview = reviewable.Contains(p.Id),
                CanEdit = editable.Contains(p.Id),
            };
            return mapped;
        }).ToArray();
    }
}
