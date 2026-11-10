using System;
using System.Net;
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
/// A post saved with a file every reader sees, put by the text only inside hidden blocks, is refused:
/// the plate would say "Все" while the text keeps the file to the readers of the blocks.
/// </summary>
/// <remarks>
/// What the author stored stays as he stored it: nothing narrows the mark behind his back, he is asked
/// to choose who sees the file.
/// </remarks>
public class PostFileInsideBlockShould : PostServiceTestBase
{
    private readonly Guid roomId = Guid.NewGuid();
    private readonly Guid mapId = Guid.NewGuid();

    private string Image => $"[img]https://dm.am/v1/uploads/{mapId:D}/content[/img]";

    private void OwnPostWithTheMap(string? addresseesJson, string storedText = "было")
    {
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = currentUserId },
            AuthorUserId = currentUserId,
            GameText = storedText,
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Empty,
            Attachments =
            [
                new PostAttachment { Id = mapId, FileName = "karta.png", AddresseesJson = addresseesJson },
            ],
        };
        repository.Get(postId, Arg.Any<Guid>()).Returns(post);
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(RoomWith(roomId, ("Вася", Guid.NewGuid())));
        repository.Update(Arg.Any<UpdatePostEntity>()).Returns(post);
    }

    private Task<Post> Save(string gameText) =>
        service.UpdateAsync(new UpdatePost { PostId = postId, GameText = gameText });

    [Fact]
    public async Task RefuseAFileForEverybodyPutOnlyInsideAHiddenBlock()
    {
        OwnPostWithTheMap(addresseesJson: null);

        var act = () => Save($"Ход. [private=\"Вася\"]{Image}[/private]");

        var refusal = (await act.Should().ThrowAsync<HttpException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Message.Should().Be(RefusalMessage.FileForAllOnlyInsideHiddenBlock("karta.png"));
        await repository.DidNotReceive().Update(Arg.Any<UpdatePostEntity>());
    }

    [Fact]
    public async Task SaveAFileForEverybodyTheTextAlsoShowsOutsideTheBlocks()
    {
        OwnPostWithTheMap(addresseesJson: null);

        await Save($"[private=\"Вася\"]{Image}[/private] И всем: {Image}");

        await repository.Received(1).Update(Arg.Any<UpdatePostEntity>());
    }

    // A file the author already marked for chosen readers is his choice, in a block or not.
    [Fact]
    public async Task SaveAMarkedFileInsideAHiddenBlock()
    {
        OwnPostWithTheMap(addresseesJson: "[]");

        await Save($"[private=\"Вася\"]{Image}[/private]");

        await repository.Received(1).Update(Arg.Any<UpdatePostEntity>());
    }

    // A save that sends no game text changes no place of a file, and is not asked about one.
    [Fact]
    public async Task NotAskWhenTheGameTextIsNotSent()
    {
        OwnPostWithTheMap(addresseesJson: null, storedText: $"[private=\"Вася\"]{Image}[/private]");

        await service.UpdateAsync(new UpdatePost { PostId = postId, MetagameText = "вне игры" });

        await repository.Received(1).Update(Arg.Any<UpdatePostEntity>());
    }
}
