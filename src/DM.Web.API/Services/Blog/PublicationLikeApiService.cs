using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Blogs.Likes;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Blog;

/// <inheritdoc />
internal class PublicationLikeApiService : IPublicationLikeApiService
{
    private readonly IPublicationLikeService _likeService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PublicationLikeApiService(
        IPublicationLikeService likeService,
        IMapper mapper)
    {
        _likeService = likeService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> LikePublication(Guid publicationId)
    {
        var likedByUser = await _likeService.LikePublication(publicationId);
        return new Envelope<User>(_mapper.Map<User>(likedByUser));
    }

    /// <inheritdoc />
    public Task DislikePublication(Guid publicationId) => _likeService.DislikePublication(publicationId);

    /// <inheritdoc />
    public async Task<Envelope<User>> LikeComment(Guid commentId)
    {
        var likedByUser = await _likeService.LikeComment(commentId);
        return new Envelope<User>(_mapper.Map<User>(likedByUser));
    }

    /// <inheritdoc />
    public Task DislikeComment(Guid commentId) => _likeService.DislikeComment(commentId);
}
