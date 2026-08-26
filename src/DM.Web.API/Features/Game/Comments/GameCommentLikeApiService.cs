using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Likes;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Comments;

/// <inheritdoc />
internal class GameCommentLikeApiService : IGameCommentLikeApiService
{
    private readonly IGameCommentLikeService _likeService;
    private readonly UserMapper _mapper;

    /// <inheritdoc />
    public GameCommentLikeApiService(
        IGameCommentLikeService likeService,
        UserMapper mapper)
    {
        _likeService = likeService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> LikeComment(Guid commentId)
    {
        var user = await _likeService.LikeCommentAsync(commentId);
        return new Envelope<User>(_mapper.ToUser(user));
    }

    /// <inheritdoc />
    public Task UnlikeComment(Guid commentId) => _likeService.UnlikeCommentAsync(commentId);
}
