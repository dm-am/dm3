using System;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

/// <summary>
/// The addressees of the [private] blocks of a post: resolved against the room
/// when the post is written, and frozen across its edits.
/// </summary>
public class PostAddresseesShould : PostServiceTestBase
{
    /// <summary>
    /// The addressee rule of BBCODE_RENDERING.md reads a field only the save path fills.
    /// </summary>
    [Fact]
    public async Task ResolvePrivateAddresseesOfANewPost()
    {
        var roomId = Guid.NewGuid();
        var annaOwner = Guid.NewGuid();
        var room = RoomWith(roomId, ("Анна", annaOwner), ("Борис", Guid.NewGuid()));
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "всем [private=\"Анна\"]только Анне[/private]"
        };

        CreatePostEntity? written = null;
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Do<CreatePostEntity>(e => written = e))
            .Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await service.CreateAsync(createPost);

        PrivateAddresseeSnapshot.Read(written!.PrivateAddresseeSnapshotJson).OwnerUserIdsByAttribute
            .Should().ContainKey("Анна")
            .WhoseValue.Should().BeEquivalentTo(new[] { annaOwner });
    }

    [Fact]
    public async Task ResolveNoPrivateAddresseeForANameOutsideTheRoom()
    {
        var roomId = Guid.NewGuid();
        var room = RoomWith(roomId, ("Анна", Guid.NewGuid()));
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "[private=\"Виктор\"]секрет[/private]"
        };

        CreatePostEntity? written = null;
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Do<CreatePostEntity>(e => written = e))
            .Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await service.CreateAsync(createPost);

        written!.PrivateAddresseeSnapshotJson.Should().Be(PrivateAddresseeSnapshot.Empty);
    }

    /// <summary>
    /// Addressee-forever survives an edit: a block resolved by the first save
    /// keeps its owner even after the character loses access to the room, while
    /// a block the edit introduced resolves against the roster it is saved with.
    /// </summary>
    [Fact]
    public async Task FreezeResolvedAddresseesAcrossAnEdit()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var annaOwner = Guid.NewGuid();
        var borisOwner = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = "[private=\"Анна\"]только Анне[/private]",
            PrivateAddresseeSnapshotJson =
                PrivateAddresseeSnapshot.Build(
                    "[private=\"Анна\"]только Анне[/private]",
                    new[] { new PrivateAddressee(Guid.NewGuid(), "Анна", annaOwner) })
        };
        // Anna has left the room by the time the post is edited; Boris has not.
        var room = RoomWith(roomId, ("Борис", borisOwner));

        UpdatePostEntity? written = null;
        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Update(Arg.Do<UpdatePostEntity>(e => written = e))
            .Returns(post);

        await service.UpdateAsync(new UpdatePost
        {
            PostId = postId,
            GameText = "[private=\"Анна\"]только Анне[/private] и [private=\"Борис\"]Борису[/private]"
        });

        var snapshot = PrivateAddresseeSnapshot.Read(written!.PrivateAddresseeSnapshotJson).OwnerUserIdsByAttribute;
        snapshot.Should().ContainKey("Анна").WhoseValue.Should().BeEquivalentTo(new[] { annaOwner });
        snapshot.Should().ContainKey("Борис").WhoseValue.Should().BeEquivalentTo(new[] { borisOwner });
    }

    /// <summary>
    /// An edit whose [private] name has changed hands since the post was saved
    /// is refused before anything is written: which character the author meant
    /// is not readable off the text.
    /// </summary>
    [Fact]
    public async Task RefuseAnEditWhosePrivateNameNowMeansAnotherCharacter()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var chuck = Guid.NewGuid();
        var annaOwner = Guid.NewGuid();
        var borisOwner = Guid.NewGuid();
        const string text = "[private=\"Чак\"]секрет[/private]";
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = text,
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Build(
                text, new[] { new PrivateAddressee(chuck, "Чак", annaOwner) })
        };
        // The character the block was frozen to has been renamed, and the name it
        // gave up now belongs to somebody else's character in the same room.
        var room = RoomWithCharacters(roomId,
            (chuck, "Владимир", annaOwner),
            (Guid.NewGuid(), "Чак", borisOwner));

        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);

        var act = () => service.UpdateAsync(new UpdatePost { PostId = postId, GameText = text });

        await act.Should().ThrowAsync<HttpException>()
            .WithMessage(PrivateAddresseeSnapshot.DescribeRenameRefusal("Чак"));
        await repository.DidNotReceive().Update(Arg.Any<UpdatePostEntity>());
    }
}
