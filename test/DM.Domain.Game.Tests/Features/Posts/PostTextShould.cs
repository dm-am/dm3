using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

/// <summary>
/// The two texts of a post, the game text and the metagame text, on their way
/// to storage: stored as typed, and who may change them.
/// </summary>
public class PostTextShould : PostServiceTestBase
{
    /// <summary>The game text as the author typed it, spaces and the case of tag names included.</summary>
    private const string GameText = "  [B]игра[/B]\n";

    /// <summary>The out-of-character text as typed, spaces around it.</summary>
    private const string MetagameText = "\n[I]вне игры[/I]  ";

    /// <summary>
    /// The save path stores both texts as typed (BBCODE_RENDERING.md, "Регистр имен"):
    /// the editor gets back exactly what was saved.
    /// </summary>
    [Fact]
    public async Task StoreBothTextsAsTypedOnCreate()
    {
        var roomId = Guid.NewGuid();
        CreatePostEntity? written = null;
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(RoomWith(roomId));
        repository.Create(Arg.Do<CreatePostEntity>(e => written = e))
            .Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await service.CreateAsync(new CreatePost
        {
            RoomId = roomId,
            GameText = GameText,
            MetagameText = MetagameText
        });

        written!.GameText.Should().Be(GameText);
        written.MetagameText.Should().Be(MetagameText);
    }

    [Fact]
    public async Task StoreBothTextsAsTypedOnEdit()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = currentUserId },
            GameText = "было",
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Empty
        };
        UpdatePostEntity? written = null;
        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(RoomWith(roomId));
        repository.Update(Arg.Do<UpdatePostEntity>(e => written = e))
            .Returns(post);

        await service.UpdateAsync(new UpdatePost
        {
            PostId = postId,
            GameText = GameText,
            MetagameText = MetagameText
        });

        written!.GameText.Should().Be(GameText);
        written.MetagameText.Should().Be(MetagameText);
    }

    /// <summary>
    /// Submitted text the caller may not edit is refused, not swapped for the
    /// stored text and saved: a 200 with the post as it was looks like a successful edit.
    /// </summary>
    [Fact]
    public async Task RefuseTextTheCallerMayNotEdit()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = "как было"
        };

        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(RoomWith(roomId));
        repository.Update(Arg.Any<UpdatePostEntity>()).Returns(post);
        intentionManager
            .When(m => m.ThrowIfForbidden(PostIntention.EditText, Arg.Any<object>()))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => service.UpdateAsync(new UpdatePost
        {
            PostId = postId,
            GameText = "как стало"
        });

        await act.Should().ThrowAsync<HttpException>();
        // A refused edit writes nothing.
        await repository.DidNotReceive().Update(Arg.Any<UpdatePostEntity>());
    }

    /// <summary>
    /// A request that submits no text asks for nothing: the lead who may change
    /// only the character must not be refused for the text he never sent.
    /// </summary>
    [Fact]
    public async Task NotAskForTextRightsWhenNoTextIsSubmitted()
    {
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            GameText = "как было"
        };

        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(RoomWith(roomId));
        repository.Update(Arg.Any<UpdatePostEntity>()).Returns(post);

        await service.UpdateAsync(new UpdatePost { PostId = postId });

        intentionManager.DidNotReceive().ThrowIfForbidden(PostIntention.EditText, Arg.Any<object>());
    }
}
