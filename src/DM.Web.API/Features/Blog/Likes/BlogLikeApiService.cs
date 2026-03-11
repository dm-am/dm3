using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Blog.Features.Likes;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Likes;

/// <inheritdoc />
internal class BlogLikeApiService : IBlogLikeApiService
{
    private readonly IBlogLikeService _likeService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BlogLikeApiService(
        IBlogLikeService likeService,
        IMapper mapper)
    {
        _likeService = likeService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<User> LikeBlogComment(Guid commentId)
    {
        var likedByUser = await _likeService.LikeBlogCommentAsync(commentId);
        return _mapper.Map<User>(likedByUser);
    }

    /// <inheritdoc />
    public Task UnlikeBlogComment(Guid commentId) => _likeService.UnlikeBlogCommentAsync(commentId);

    /// <inheritdoc />
    public async Task<Envelope<User>> LikePublicationComment(Guid commentId)
    {
        var likedByUser = await _likeService.LikePublicationCommentAsync(commentId);
        return new Envelope<User>(_mapper.Map<User>(likedByUser));
    }

    /// <inheritdoc />
    public Task UnlikePublicationComment(Guid commentId) => _likeService.UnlikePublicationCommentAsync(commentId);

    /// <inheritdoc />
    public async Task<Envelope<User>> LikePublication(Guid publicationId)
    {
        var likedByUser = await _likeService.LikePublicationAsync(publicationId);
        return new Envelope<User>(_mapper.Map<User>(likedByUser));
    }

    /// <inheritdoc />
    public Task UnlikePublication(Guid publicationId) => _likeService.UnlikePublicationAsync(publicationId);
}
