using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Core.Content;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

/// <summary>
/// Who may put a file on a post, read one off it, and take one off it.
/// </summary>
/// <remarks>
/// Three different answers to what looks like one question, and the reason they
/// differ is the point of this file. Attaching is authorship, so it is the author
/// alone. Reading is visibility, so it is whoever may read the post. Removing is
/// editing what a room contains, so it follows the post's edit rule and reaches
/// the master.
///
/// Existence is never a separate check: every path goes through
/// PostRepository.Get with the caller's own id, which is the read that applies
/// the room filter, so an invisible post is absent rather than refused.
/// </remarks>
public class PostAttachmentUploadAuthorizerShould : UnitTestBase
{
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly Guid postId = Guid.NewGuid();
    private readonly Guid roomId = Guid.NewGuid();
    private readonly Guid uploadId = Guid.NewGuid();

    private readonly IPostRepository postRepository;
    private readonly IRoomRepository roomRepository;
    private readonly IIntentionManager intentionManager;
    private readonly IPostAttachmentCarrierRepository carrierRepository;
    private readonly PostAttachmentUploadAuthorizer sut;

    public PostAttachmentUploadAuthorizerShould()
    {
        postRepository = Mock<IPostRepository>();
        roomRepository = Mock<IRoomRepository>();
        intentionManager = Mock<IIntentionManager>();
        carrierRepository = Mock<IPostAttachmentCarrierRepository>();

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId));

        sut = new PostAttachmentUploadAuthorizer(
            postRepository,
            roomRepository,
            intentionManager,
            identityProvider,
            new PrivateBlockReading(new BbParserProvider()),
            carrierRepository);
    }

    /// <summary>A post the caller can see, written by whoever is named.</summary>
    private void PostVisible(Guid authorId) =>
        postRepository.Get(postId, currentUserId).Returns(new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = authorId, Username = "author" },
            AuthorUserId = authorId,
            GameText = "текст",
            MetagameText = string.Empty,
        });

    /// <summary>
    /// A post of somebody else the caller can see, whose text shows the file only inside a block
    /// addressed to the reader named.
    /// </summary>
    private void PostShowingTheFileOnlyInABlockFor(Guid addresseeId) =>
        postRepository.Get(postId, currentUserId).Returns(LetterFor(addresseeId));

    private Post LetterFor(Guid addresseeId) => new()
    {
        Id = postId,
        RoomId = roomId,
        Author = new GeneralUser { UserId = Guid.NewGuid(), Username = "author" },
        AuthorUserId = Guid.NewGuid(),
        GameText = $"Передаю письмо. [private=\"Горим\"][img]https://dm.am/v1/uploads/{uploadId:D}/content[/img][/private]",
        MetagameText = string.Empty,
        PrivateAddresseeSnapshotJson = $"{{\"Горим\":[{{\"owner\":\"{addresseeId}\"}}]}}",
    };

    /// <summary>The file asked about, on the post, marked for everybody unless a mark is named.</summary>
    private StoredUpload Upload(string? addresseesJson = null) => new()
    {
        Id = uploadId,
        TargetId = postId,
        Type = UploadType.PostAttachment,
        AddresseesJson = addresseesJson,
    };

    /// <summary>A post outside the caller's scope: the read answers with nothing.</summary>
    private void PostInvisible() =>
        postRepository.Get(postId, currentUserId).Returns((Post?)null);

    private void RoomAvailable() =>
        roomRepository.GetForUpdate(roomId, currentUserId).Returns(new RoomToUpdate { Id = roomId });

    [Fact]
    public async Task LetTheAuthorAttachAFileToTheirOwnPost()
    {
        PostVisible(authorId: currentUserId);

        var act = () => sut.EnsureAllowedAsync(postId);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The rule this narrows. Being allowed to write in the room is what creates
    /// a post of one's own; it is not permission to add material to somebody
    /// else's, which would publish a file under their name.
    /// </summary>
    [Fact]
    public async Task RefuseAnybodyElseAttachingToAPostTheyDidNotWrite()
    {
        PostVisible(authorId: Guid.NewGuid());

        var act = () => sut.EnsureAllowedAsync(postId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The room's write permission is never consulted, so the master of the game
    /// is refused here exactly as any other non-author is.
    /// </summary>
    [Fact]
    public async Task NeverAskWhetherTheCallerMayPostInTheRoom()
    {
        PostVisible(authorId: Guid.NewGuid());

        var act = () => sut.EnsureAllowedAsync(postId);

        await act.Should().ThrowAsync<HttpException>();
        await roomRepository.DidNotReceive().GetForUpdate(Arg.Any<Guid>(), Arg.Any<Guid>());
        intentionManager.DidNotReceive().ThrowIfForbidden(Arg.Any<RoomIntention>(), Arg.Any<object>());
    }

    [Fact]
    public async Task AnswerNotFoundForAPostTheCallerCannotSee()
    {
        PostInvisible();

        var act = () => sut.EnsureAllowedAsync(postId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LetAnyReaderOfThePostReadItsAttachment()
    {
        PostVisible(authorId: Guid.NewGuid());

        var act = () => sut.EnsureReadAllowedAsync(Upload());

        await act.Should().NotThrowAsync(
            "the file is visible wherever the post is, which is not only to its author");
    }

    /// <summary>
    /// 404, not 403. A refusal that distinguishes "no such file" from "not for
    /// you" tells an outsider that a private game has one.
    /// </summary>
    [Fact]
    public async Task AnswerNotFoundWhenReadingAnAttachmentOfAnInvisiblePost()
    {
        PostInvisible();

        var act = () => sut.EnsureReadAllowedAsync(Upload());

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The read is scoped to the caller. Passing anything else — an empty id, a
    /// system identity — would make every closed room readable through this one
    /// method while every other read of the same post stayed shut.
    /// </summary>
    [Fact]
    public async Task ReadThePostInTheCallersOwnScope()
    {
        PostVisible(authorId: currentUserId);

        await sut.EnsureReadAllowedAsync(Upload());

        await postRepository.Received(1).Get(postId, currentUserId);
        await postRepository.DidNotReceive().Get(Arg.Any<Guid>(), Arg.Is<Guid>(id => id != currentUserId));
    }

    /// <summary>
    /// A reader of the post the block is not addressed to: the file is as absent to him as one of
    /// a post he cannot see, and the refusal names the file, not the post he does see.
    /// </summary>
    [Fact]
    public async Task AnswerNotFoundForAFileShownOnlyInABlockNotAddressedToTheCaller()
    {
        PostShowingTheFileOnlyInABlockFor(addresseeId: Guid.NewGuid());

        var act = () => sut.EnsureReadAllowedAsync(Upload());

        var refusal = (await act.Should().ThrowAsync<HttpException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Message.Should().Be(RefusalMessage.UploadNotFound);
    }

    [Fact]
    public async Task LetTheAddresseeOfTheBlockReadTheFileInIt()
    {
        PostShowingTheFileOnlyInABlockFor(addresseeId: currentUserId);

        var act = () => sut.EnsureReadAllowedAsync(Upload());

        await act.Should().NotThrowAsync("the block is open to him, and the file with it");
    }

    /// <summary>
    /// The file's own right lets Moderator+ past the room, and not past a hidden block: the post is read
    /// wherever it stands, and the file in a block he does not read is refused him as to anybody.
    /// </summary>
    [Fact]
    public async Task KeepAFileShownOnlyInABlockFromACallerTheFileRightLetsPastTheRoom()
    {
        carrierRepository.GetCarriers(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, Post> { [postId] = LetterFor(addresseeId: Guid.NewGuid()) });

        (await sut.WithheldAsync([Upload()])).Should().Equal(uploadId);
        await postRepository.DidNotReceive().Get(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    /// <summary>A file whose post is gone fails closed: no text is left to say it was not kept.</summary>
    [Fact]
    public async Task KeepAFileWhosePostIsGoneFromACallerTheFileRightLetsPastTheRoom()
    {
        carrierRepository.GetCarriers(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, Post>());

        (await sut.WithheldAsync([Upload()])).Should().Equal(uploadId);
    }

    private static readonly Guid VasyaCharacterId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid VasyaOwnerId = Guid.Parse("00000000-0000-0000-0000-0000000000d2");

    private static readonly PrivateAddressee Vasya = new(VasyaCharacterId, "Вася", VasyaOwnerId);

    /// <summary>A character of the game no longer let into the room.</summary>
    private static readonly PrivateAddressee Gone = new(
        Guid.Parse("00000000-0000-0000-0000-0000000000d3"), "Ушедший", Guid.Parse("00000000-0000-0000-0000-0000000000d4"));

    /// <summary>The mark of a file as the upload path stores it.</summary>
    private static string MarkFor(params PrivateAddressee[] characters) =>
        "[" + string.Join(",", characters.Select(c =>
            $"{{\"character\":\"{c.CharacterId}\",\"name\":\"{c.CharacterName}\",\"owner\":\"{c.OwnerUserId}\"}}")) + "]";

    /// <summary>The room with the addressee let into it.</summary>
    private void RoomWithVasya() =>
        roomRepository.GetForUpdate(roomId, currentUserId).Returns(new RoomToUpdate
        {
            Id = roomId,
            Accesses =
            [
                new RoomAccess
                {
                    Character = new Character
                    {
                        Id = VasyaCharacterId,
                        Name = "Вася",
                        Author = new GeneralUser { UserId = VasyaOwnerId, Username = "vasya" },
                    },
                },
            ],
        });

    /// <summary>
    /// The post of the caller, carrying the file asked about under the name given, and another file when
    /// its mark is named.
    /// </summary>
    private void OwnPostWithTheFile(string gameText, string? addresseesJson = null, string? anotherFileMark = null)
    {
        List<PostAttachment> attachments = [Attachment(uploadId, "karta.png", addresseesJson)];
        if (anotherFileMark != null) attachments.Add(Attachment(Guid.NewGuid(), "pismo.png", anotherFileMark));

        postRepository.Get(postId, currentUserId).Returns(new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = currentUserId, Username = "author" },
            AuthorUserId = currentUserId,
            GameText = gameText,
            MetagameText = string.Empty,
            Attachments = attachments,
        });
    }

    private static PostAttachment Attachment(Guid id, string fileName, string? addresseesJson) =>
        new() { Id = id, FileName = fileName, ContentType = "image/png", AddresseesJson = addresseesJson };

    /// <summary>A new file for everybody needs nothing read: nothing is frozen and nothing mentions it yet.</summary>
    [Fact]
    public async Task StoreNoMarkForANewFileForEverybody()
    {
        (await sut.FreezeAudienceAsync(postId, null, UploadAudience.All)).Should().BeNull();
        await postRepository.DidNotReceive().Get(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    /// <summary>The characters chosen are frozen with their owners from the roster of the room.</summary>
    [Fact]
    public async Task FreezeTheChosenCharacterWithItsOwnerFromTheRoom()
    {
        PostVisible(authorId: currentUserId);
        RoomWithVasya();

        var mark = await sut.FreezeAudienceAsync(
            postId, null, new UploadAudience(UploadVisibleTo.Selected, [VasyaCharacterId]));

        var addressees = PrivateAddresseeSnapshot.ReadList(mark);
        addressees.Should().NotBeNull();
        addressees!.Characters.Should().Equal(Vasya);
        addressees.OwnerUserIds.Should().BeEquivalentTo(new[] { VasyaOwnerId });
        addressees.Names.Should().Equal("Вася");
    }

    /// <summary>"Выбранные" without a character is a mark for nobody: the author and the leads.</summary>
    [Fact]
    public async Task StoreAMarkForNobodyWhenNoCharacterIsChosen()
    {
        PostVisible(authorId: currentUserId);
        RoomWithVasya();

        var mark = await sut.FreezeAudienceAsync(postId, null, new UploadAudience(UploadVisibleTo.Selected, []));

        mark.Should().Be("[]");
        PrivateAddresseeSnapshot.ReadList(mark)!.NamesAddressees.Should().BeFalse();
    }

    /// <summary>A character outside the room is refused by name of the rule, not dropped.</summary>
    [Fact]
    public async Task RefuseACharacterNotLetIntoTheRoom()
    {
        PostVisible(authorId: currentUserId);
        RoomWithVasya();

        var act = () => sut.FreezeAudienceAsync(
            postId, null, new UploadAudience(UploadVisibleTo.Selected, [VasyaCharacterId, Guid.NewGuid()]));

        var refusal = (await act.Should().ThrowAsync<HttpException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Message.Should().Be(RefusalMessage.CharacterNotInRoom);
    }

    /// <summary>
    /// A character gone from the room stays on the file whose mark names him while his box stays checked,
    /// as the mark froze him; the one still in the room is frozen from the room.
    /// </summary>
    [Fact]
    public async Task KeepACharacterGoneFromTheRoomOnTheFileWhoseMarkNamesHim()
    {
        OwnPostWithTheFile("текст", addresseesJson: MarkFor(Gone, Vasya with { CharacterName = "Василий" }));
        RoomWithVasya();

        var mark = await sut.FreezeAudienceAsync(
            postId, uploadId, new UploadAudience(UploadVisibleTo.Selected, [VasyaCharacterId, Gone.CharacterId]));

        PrivateAddresseeSnapshot.ReadList(mark)!.Characters.Should().BeEquivalentTo(new[] { Gone, Vasya });
    }

    /// <summary>Only the mark of the file itself keeps him: another file of the post lets him into nothing.</summary>
    [Fact]
    public async Task RefuseACharacterGoneFromTheRoomThatTheMarkOfTheFileDoesNotName()
    {
        OwnPostWithTheFile("текст", addresseesJson: MarkFor(Vasya), anotherFileMark: MarkFor(Gone));
        RoomWithVasya();

        var act = () => sut.FreezeAudienceAsync(
            postId, uploadId, new UploadAudience(UploadVisibleTo.Selected, [VasyaCharacterId, Gone.CharacterId]));

        (await act.Should().ThrowAsync<HttpException>()).Which.Message.Should().Be(RefusalMessage.CharacterNotInRoom);
    }

    /// <summary>
    /// "Все" for a file the text keeps only inside a block would say two things at once, so the author is
    /// asked to choose; a file the text shows outside a block is everybody's to have.
    /// </summary>
    [Fact]
    public async Task RefuseEverybodyForAFileTheTextPutsOnlyInsideHiddenBlocks()
    {
        OwnPostWithTheFile($"[private=\"Вася\"][img]https://dm.am/v1/uploads/{uploadId:D}/content[/img][/private]",
            addresseesJson: "[]");

        var act = () => sut.FreezeAudienceAsync(postId, uploadId, UploadAudience.All);

        var refusal = (await act.Should().ThrowAsync<HttpException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Message.Should().Be(RefusalMessage.FileForAllOnlyInsideHiddenBlock("karta.png"));
    }

    [Fact]
    public async Task LetEverybodyHaveAFileTheTextShowsOutsideTheBlocks()
    {
        OwnPostWithTheFile(
            $"[private=\"Вася\"]тайна[/private] Карта: [img]https://dm.am/v1/uploads/{uploadId:D}/content[/img]",
            addresseesJson: "[]");

        (await sut.FreezeAudienceAsync(postId, uploadId, UploadAudience.All)).Should().BeNull();
    }

    /// <summary>
    /// The file's own right lets Moderator+ past the room and not past a mark: a file chosen for the addressee
    /// is kept from him, one for everybody is not, one question for the page.
    /// </summary>
    [Fact]
    public async Task KeepAMarkedFileFromACallerTheFileRightLetsPastTheRoom()
    {
        var forEverybody = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            RoomId = roomId,
            Author = new GeneralUser { UserId = Guid.NewGuid(), Username = "author" },
            GameText = "текст",
            MetagameText = string.Empty,
        };
        carrierRepository.GetCarriers(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, Post> { [postId] = post });
        var forVasya = Upload($"[{{\"character\":\"{VasyaCharacterId}\",\"name\":\"Вася\",\"owner\":\"{VasyaOwnerId}\"}}]");
        var open = new StoredUpload { Id = forEverybody, TargetId = postId, Type = UploadType.PostAttachment };

        (await sut.WithheldAsync([forVasya, open])).Should().Equal(uploadId);
        await carrierRepository.Received(1).GetCarriers(Arg.Any<IReadOnlyCollection<Guid>>());
    }

    /// <summary>The address of a marked file answers a reader of the post it was not chosen for as a missing one.</summary>
    [Fact]
    public async Task AnswerNotFoundForAMarkedFileToAReaderNotChosen()
    {
        PostVisible(authorId: Guid.NewGuid());

        var act = () => sut.EnsureReadAllowedAsync(Upload("[]"));

        var refusal = (await act.Should().ThrowAsync<HttpException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Message.Should().Be(RefusalMessage.UploadNotFound);
    }

    [Fact]
    public async Task LetWhoeverMayEditThePostTakeAFileOffIt()
    {
        PostVisible(authorId: Guid.NewGuid());
        RoomAvailable();
        intentionManager
            .IsAllowed(PostIntention.EditText, Arg.Any<(Post, RoomToUpdate)>()).Returns(true);

        (await sut.MayDetachAsync(postId)).Should().BeTrue();
    }

    [Fact]
    public async Task GrantNoRemovalRightWhenThePostMayNotBeEdited()
    {
        PostVisible(authorId: Guid.NewGuid());
        RoomAvailable();
        intentionManager
            .IsAllowed(PostIntention.EditText, Arg.Any<(Post, RoomToUpdate)>()).Returns(false);

        (await sut.MayDetachAsync(postId)).Should().BeFalse();
    }

    [Fact]
    public async Task GrantNoRemovalRightOverAPostTheCallerCannotSee()
    {
        PostInvisible();

        (await sut.MayDetachAsync(postId)).Should().BeFalse();
    }
}
