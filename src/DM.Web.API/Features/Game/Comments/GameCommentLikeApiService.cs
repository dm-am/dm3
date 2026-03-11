using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Game.Features.Likes;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Comments;

/// <inheritdoc />
internal class GameCommentLikeApiService : IGameCommentLikeApiService
{
    private readonly IGameCommentLikeService _likeService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GameCommentLikeApiService(
        IGameCommentLikeService likeService,
        IMapper mapper)
    {
        _likeService = likeService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<User> LikeComment(Guid commentId)
    {
        var user = await _likeService.LikeCommentAsync(commentId);
        return _mapper.Map<User>(user);
    }

    /// <inheritdoc />
    public Task UnlikeComment(Guid commentId) => _likeService.UnlikeCommentAsync(commentId);
}
