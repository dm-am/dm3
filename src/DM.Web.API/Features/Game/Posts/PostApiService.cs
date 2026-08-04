using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Posts;
using DM.Web.API.Shared.Dto;
using CreatePost = DM.Domain.Game.Features.Posts.CreatePost;
using UpdatePost = DM.Domain.Game.Features.Posts.UpdatePost;

namespace DM.Web.API.Features.Game.Posts;

/// <inheritdoc />
internal class PostApiService : IPostApiService
{
    private readonly IPostService _postService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PostApiService(
        IPostService postService,
        IMapper mapper)
    {
        _postService = postService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Post>> Get(Guid roomId, PagingQuery query)
    {
        var (posts, paging) = await _postService.GetAllAsync(roomId, query);
        return new ListEnvelope<Post>(posts.Select(_mapper.Map<Post>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Post>> Get(Guid postId)
    {
        var post = await _postService.GetAsync(postId);
        return new Envelope<Post>(_mapper.Map<Post>(post));
    }

    /// <inheritdoc />
    public async Task<Envelope<Post>> Create(Guid roomId, CreatePostRequest post)
    {
        var createPost = _mapper.Map<CreatePost>(post);
        createPost.RoomId = roomId;
        var createdPost = await _postService.CreateAsync(createPost);
        return new Envelope<Post>(_mapper.Map<Post>(createdPost));
    }

    /// <inheritdoc />
    public async Task<Envelope<Post>> Update(Guid postId, UpdatePostRequest request)
    {
        var updatePost = _mapper.Map<UpdatePost>(request);
        updatePost.PostId = postId;
        var updatedPost = await _postService.UpdateAsync(updatePost);
        return new Envelope<Post>(_mapper.Map<Post>(updatedPost));
    }

    /// <inheritdoc />
    public Task Delete(Guid postId) => _postService.DeleteAsync(postId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid roomId) => _postService.MarkAsReadAsync(roomId);

    /// <inheritdoc />
    public async Task<ListEnvelope<Post>> GetRated(PostsQuery query)
    {
        var (posts, paging) = await _postService.GetRatedAsync(query);
        return new ListEnvelope<Post>(posts.Select(_mapper.Map<Post>), new PagingInfo(paging));
    }
}
