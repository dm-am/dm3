using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Likes;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Likes;

public class BlogLikeApiService
{
    private readonly IBlogLikeService _likeService;
    private readonly UserMapper _mapper;

    public BlogLikeApiService(
        IBlogLikeService likeService,
        UserMapper mapper)
    {
        _likeService = likeService;
        _mapper = mapper;
    }

    public async Task<User> LikeBlogComment(Guid commentId)
    {
        var likedByUser = await _likeService.LikeBlogCommentAsync(commentId);
        return _mapper.ToUser(likedByUser);
    }

    public Task UnlikeBlogComment(Guid commentId) => _likeService.UnlikeBlogCommentAsync(commentId);

    public async Task<Envelope<User>> LikePublicationComment(Guid commentId)
    {
        var likedByUser = await _likeService.LikePublicationCommentAsync(commentId);
        return new Envelope<User>(_mapper.ToUser(likedByUser));
    }

    public Task UnlikePublicationComment(Guid commentId) => _likeService.UnlikePublicationCommentAsync(commentId);

    public async Task<Envelope<User>> LikePublication(Guid publicationId)
    {
        var likedByUser = await _likeService.LikePublicationAsync(publicationId);
        return new Envelope<User>(_mapper.ToUser(likedByUser));
    }

    public Task UnlikePublication(Guid publicationId) => _likeService.UnlikePublicationAsync(publicationId);
}
