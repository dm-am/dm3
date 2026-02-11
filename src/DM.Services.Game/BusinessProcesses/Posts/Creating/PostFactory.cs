using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.Posts.Creating;

/// <inheritdoc />
internal class PostFactory : IPostFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PostFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Post Create(CreatePost createPost, Guid userId)
    {
        return new Post
        {
            PostId = _guidFactory.Create(),
            CreatedUtc = _dateTimeProvider.Now,
            UserId = userId,
            RoomId = createPost.RoomId,
            CharacterId = createPost.CharacterId,
            Text = createPost.Text,
            Commentary = createPost.Commentary,
            MasterMessage = createPost.MasterMessage,
            IsRemoved = false,
            ModifiedUtc = null
        };
    }
}