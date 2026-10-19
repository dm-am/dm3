using System;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Likes;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Comments;

public class GameCommentLikeApiService
{
    private readonly IGameCommentLikeService _likeService;
    private readonly UserMapper _mapper;

    public GameCommentLikeApiService(
        IGameCommentLikeService likeService,
        UserMapper mapper)
    {
        _likeService = likeService;
        _mapper = mapper;
    }

    public async Task<Envelope<User>> LikeComment(Guid commentId)
    {
        var user = await _likeService.LikeCommentAsync(commentId);
        return new Envelope<User>(_mapper.ToUser(user));
    }

    public Task UnlikeComment(Guid commentId) => _likeService.UnlikeCommentAsync(commentId);
}
