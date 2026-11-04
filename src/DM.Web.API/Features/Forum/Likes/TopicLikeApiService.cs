using System;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Likes;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Forum.Likes;

public class TopicLikeApiService
{
    private readonly ITopicLikeService _likeService;
    private readonly UserMapper _mapper;

    public TopicLikeApiService(
        ITopicLikeService likeService,
        UserMapper mapper)
    {
        _likeService = likeService;
        _mapper = mapper;
    }

    public async Task<Envelope<User>> LikeTopic(Guid topicId)
    {
        var likedByUser = await _likeService.LikeTopicAsync(topicId);
        return new Envelope<User>(_mapper.ToUser(likedByUser));
    }

    public Task UnlikeTopic(Guid topicId) => _likeService.UnlikeTopicAsync(topicId);

    public async Task<Envelope<User>> LikeComment(Guid commentId)
    {
        var likedByUser = await _likeService.LikeCommentAsync(commentId);
        return new Envelope<User>(_mapper.ToUser(likedByUser));
    }

    public Task UnlikeComment(Guid commentId) => _likeService.UnlikeCommentAsync(commentId);
}
