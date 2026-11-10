using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Tests.Dsl;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class PostServiceShould : PostServiceTestBase
{
    [Fact]
    public async Task AuthorizeCreatePostAction()
    {
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, CharacterId = characterId, GameText = "Test post" };
        var room = RoomWith(roomId);

        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Any<CreatePostEntity>()).Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await service.CreateAsync(createPost);

        intentionManager.Received(1).ThrowIfForbidden(
            RoomIntention.CreatePost,
            Arg.Is<(RoomToUpdate, Guid?)>(t => t.Item1 == room && t.Item2 == characterId));
    }

    [Fact]
    public async Task CreatePostAndIncrementUnreadCounters()
    {
        var roomId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, GameText = "Test post" };
        var room = RoomWith(roomId);
        var createdPost = new Post { Id = Guid.NewGuid(), RoomId = roomId };

        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Any<CreatePostEntity>()).Returns(createdPost);

        await service.CreateAsync(createPost);

        await repository.Received(1).Create(Arg.Any<CreatePostEntity>());
        await unreadCountersRepository.Received(1).IncrementAsync(roomId, UnreadEntryType.Message);
    }

    [Fact]
    public async Task PublishNewPostEvent()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var createPost = new CreatePost { RoomId = roomId, GameText = "Test post" };
        var room = RoomWith(roomId);

        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Any<CreatePostEntity>()).Returns(new Post { Id = postId, RoomId = roomId });

        await service.CreateAsync(createPost);

        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(events => events.Contains(EventType.NewPost)), postId);
    }

    [Fact]
    public async Task AuthorizeDeletePostAction()
    {
        var postId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = Guid.NewGuid(),
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            CreatedUtc = DateTimeOffset.UtcNow
        };

        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        repository.Delete(postId, currentUserId).Returns(true);

        await service.DeleteAsync(postId);

        intentionManager.Received(1).ThrowIfForbidden(PostIntention.Delete, post);
    }

    [Fact]
    public async Task DeletePostAndDecrementUnreadCounters()
    {
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow;
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = authorId },
            CreatedUtc = createdUtc
        };

        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        repository.Delete(postId, currentUserId).Returns(true);

        await service.DeleteAsync(postId);

        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await repository.Received(1).Delete(postId, currentUserId);
        await unreadCountersRepository.Received(1).DecrementAsync(roomId, UnreadEntryType.Message, createdUtc);
    }

    /// <remarks>
    /// The write reports whether it is the one that removed the post, and the
    /// unread counter of the room is a stored number that nothing recomputes:
    /// lowered once per request rather than once per post, two removals arriving
    /// together leave every member of the room one unread message short for good.
    /// </remarks>
    [Fact]
    public async Task LeaveTheUnreadCountersToTheRemovalThatTookThePost()
    {
        var postId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            CreatedUtc = DateTimeOffset.UtcNow
        };

        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        repository.Delete(postId, currentUserId).Returns(false);

        await service.DeleteAsync(postId);

        await unreadCountersRepository.DidNotReceive()
            .DecrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<DateTimeOffset>());
        await producer.DidNotReceive().SendAsync(EventType.DeletedPost, postId);
    }
}
