using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Posts;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using DomainCharacterShort = DM.Domain.Game.Features.Characters.CharacterShort;
using DomainPost = DM.Domain.Game.Features.Posts.Post;
using DomainPostAttachment = DM.Domain.Game.Features.Posts.PostAttachment;
using PostAttachmentVisibility = DM.Domain.Game.Features.Posts.PostAttachmentVisibility;
using UploadAddressee = DM.Web.API.Shared.Dto.UploadAddressee;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// An attachment the game text shows only inside hidden blocks is read by those who read a block,
/// and every other attachment by every reader of the post.
/// </summary>
/// <remarks>
/// The rule runs on the parser and the visibility filter of the page, so every case here is a text
/// the page would render, and a reader the filter would answer for.
/// </remarks>
public class PostAttachmentVisibilityShould : UnitTestBase
{
    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid MasterId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid AssistantId = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid AddresseeId = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
    private static readonly Guid SecondAddresseeId = Guid.Parse("00000000-0000-0000-0000-0000000000a5");
    private static readonly Guid ReaderId = Guid.Parse("00000000-0000-0000-0000-0000000000a6");

    private static readonly Guid MapId = Guid.Parse("571c0beb-1890-9ba6-2170-4bb531bc51f6");
    private static readonly Guid LetterId = Guid.Parse("8d0a5f3e-2b47-4c19-9e6a-3f1b7c2d4e58");

    private static readonly PrivateBlockReading Rule = new(new BbParserProvider());

    private static string Address(Guid uploadId) => $"https://dm.am/v1/uploads/{uploadId:D}/content";

    private static string Image(Guid uploadId) => $"[img]{Address(uploadId)}[/img]";

    private static IAuthorizationSubject Reader(Guid userId) =>
        new AuthenticatedUser { UserId = userId, Username = "reader", Role = UserRole.RegularUser };

    private static DomainPost PostWith(
        string gameText, bool gameShowsAll = false, bool roomViewPrivate = false, string? mapMark = null) => new()
        {
            Id = Guid.NewGuid(),
            RoomId = Guid.NewGuid(),
            AuthorUserId = AuthorId,
            GameId = Guid.NewGuid(),
            CreatedUtc = DateTimeOffset.UtcNow,
            GameText = gameText,
            MetagameText = string.Empty,
            PrivateAddresseeSnapshotJson =
            $"{{\"Горим\":[{{\"owner\":\"{AddresseeId}\"}}],\"Леголас\":[{{\"owner\":\"{SecondAddresseeId}\"}}]}}",
            GameMasterUserId = MasterId,
            GameAssistantUserIds = [AssistantId],
            GameShowPrivateMessages = gameShowsAll,
            RoomViewPrivateText = roomViewPrivate,
            Author = new GeneralUser { UserId = AuthorId, Username = "author" },
            Character = new DomainCharacterShort
            {
                Id = Guid.NewGuid(),
                Author = new GeneralUser { UserId = AuthorId, Username = "author" },
                Name = "Арагорн"
            },
            Attachments = [Attachment(MapId, mapMark), Attachment(LetterId)]
        };

    private static DomainPostAttachment Attachment(Guid id, string? addresseesJson = null) => new()
    {
        Id = id,
        AddresseesJson = addresseesJson,
        FileName = "karta.png",
        ContentType = "image/png",
        SizeBytes = 6707,
        CreatedUtc = DateTimeOffset.UtcNow,
    };

    private static readonly string MapForGorim = $"Он протянул карту. [private=\"Горим\"]{Image(MapId)}[/private]";

    private static bool Gets(IAuthorizationSubject? reader, DomainPost post, Guid attachmentId) =>
        !PostAttachmentVisibility.HiddenFrom(Rule, reader, post).Contains(attachmentId);

    [Fact]
    public void GiveAFileShownOnlyInsideABlockToItsAddressee() =>
        Gets(Reader(AddresseeId), PostWith(MapForGorim), MapId).Should().BeTrue();

    [Fact]
    public void KeepSuchAFileFromAReaderTheBlockIsNotAddressedTo() =>
        Gets(Reader(ReaderId), PostWith(MapForGorim), MapId).Should().BeFalse(
            "a picture under the post would say what the block keeps from him");

    [Fact]
    public void KeepSuchAFileFromAGuest() =>
        Gets(null, PostWith(MapForGorim), MapId).Should().BeFalse();

    [Fact]
    public void GiveSuchAFileToTheAuthor() =>
        Gets(Reader(AuthorId), PostWith(MapForGorim), MapId).Should().BeTrue();

    [Fact]
    public void GiveSuchAFileToTheMasterAndTheAssistant()
    {
        var post = PostWith(MapForGorim);

        Gets(Reader(MasterId), post, MapId).Should().BeTrue();
        Gets(Reader(AssistantId), post, MapId).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GiveSuchAFileToTheRoomWhereAnOverrideOpensTheBlock(bool gameShowsAll, bool roomViewPrivate) =>
        Gets(Reader(ReaderId), PostWith(MapForGorim, gameShowsAll, roomViewPrivate), MapId).Should().BeTrue();

    [Fact]
    public void GiveAFileMentionedOutsideTheBlockToo()
    {
        var post = PostWith($"{MapForGorim} Всем: [link]{Address(MapId)}[/link]");

        Gets(Reader(ReaderId), post, MapId).Should().BeTrue("the text shows the file to the whole room");
        Gets(null, post, MapId).Should().BeTrue();
    }

    [Fact]
    public void GiveAFileTheTextDoesNotMentionToEveryReader()
    {
        var post = PostWith("Он наклонился к ней. [private=\"Горим\"]Беги.[/private]");

        Gets(Reader(ReaderId), post, MapId).Should().BeTrue("a block keeps only what is in it");
        Gets(null, post, MapId).Should().BeTrue();
    }

    [Fact]
    public void KeepEachOfTwoFilesInTwoBlocksForItsOwnAddressee()
    {
        var post = PostWith(
            $"[private=\"Горим\"]{Image(MapId)}[/private] [private=\"Леголас\"]{Image(LetterId)}[/private]");
        Guid[] both = [MapId, LetterId];

        PostAttachmentVisibility.HiddenFrom(Rule, Reader(AddresseeId), post)
            .Should().BeEquivalentTo(new[] { LetterId });
        PostAttachmentVisibility.HiddenFrom(Rule, Reader(SecondAddresseeId), post)
            .Should().BeEquivalentTo(new[] { MapId });
        PostAttachmentVisibility.HiddenFrom(Rule, Reader(ReaderId), post)
            .Should().BeEquivalentTo(both);
    }

    // The address serves the file under either spelling of the identifier, so either one inside a
    // block is a mention there; missing it would show the file to the whole room.
    [Theory]
    [InlineData("D-upper")]
    [InlineData("N")]
    public void ReadTheIdentifierInEitherSpellingAndAnyCase(string spelling)
    {
        var written = spelling == "N" ? MapId.ToString("N") : MapId.ToString("D").ToUpperInvariant();
        var post = PostWith($"[private=\"Горим\"][img]https://dm.am/v1/uploads/{written}/content[/img][/private]");

        Gets(Reader(ReaderId), post, MapId).Should().BeFalse();
    }

    // A block that names nobody, bare or misspelled, is read by the author and the leads alone:
    // neither override opens it, nor the file in it.
    [Theory]
    [InlineData("[private]")]
    [InlineData("[private=Горим]")]
    public void KeepAFileInABlockThatNamesNobodyFromTheRoomWhateverTheOverrides(string opening)
    {
        var post = PostWith($"{opening}{Image(MapId)}[/private]", gameShowsAll: true, roomViewPrivate: true);

        Gets(Reader(ReaderId), post, MapId).Should().BeFalse("the overrides share what is addressed to characters");
        Gets(Reader(MasterId), post, MapId).Should().BeTrue();
    }

    // A text the parser refuses is rendered as nothing, to its author as well, so nobody is shown a
    // file it mentions.
    [Fact]
    public void KeepAFileOfATextTheParserRefusesFromEveryReader()
    {
        var tooDeep = string.Concat(Enumerable.Repeat("[b]", 600));
        var post = PostWith($"[private=\"Горим\"]Беги.[/private] {tooDeep}{Image(MapId)}");

        Gets(Reader(AuthorId), post, MapId).Should().BeFalse("the page shows its author nothing of the text either");
        Gets(Reader(MasterId), post, MapId).Should().BeFalse();
    }

    // The tag set has no [url], so the parameter is the text of [link].
    [Fact]
    public void ReadAMentionInAParameterOfATagInsideTheBlock()
    {
        var post = PostWith($"[private=\"Горим\"][link text=\"{Address(MapId)}\"]https://example.com/[/link][/private]");

        Gets(Reader(ReaderId), post, MapId).Should().BeFalse();
        Gets(Reader(AddresseeId), post, MapId).Should().BeTrue();
    }

    // A field one of the two forgot would show the picture where the strip hides it, or the other
    // way round.
    [Fact]
    public void ListAFileExactlyWhereThePageShowsIt()
    {
        IAuthorizationSubject[] readers =
        [
            Reader(AuthorId), Reader(MasterId), Reader(AssistantId), Reader(AddresseeId),
            Reader(SecondAddresseeId), Reader(ReaderId), AuthenticatedUser.Guest
        ];
        var disagreements = new List<string>();

        var cases =
            from opening in new[] { "[private=\"Горим\"]", "[private]" }
            from overrides in new[] { (Game: false, Room: false), (Game: true, Room: false), (Game: false, Room: true) }
            from reader in readers
            select (opening, overrides.Game, overrides.Room, reader);

        foreach (var (opening, gameShowsAll, roomViewPrivate, reader) in cases)
        {
            var post = PostWith($"{opening}{Image(MapId)}[/private]", gameShowsAll, roomViewPrivate);
            var mapped = MapperFor(reader).ToPost(post);

            var listed = mapped.Attachments.Any(a => a.Id == MapId);
            var shown = Rendered(mapped.GameText, reader).Contains(MapId.ToString("D"), StringComparison.OrdinalIgnoreCase);
            if (listed != shown)
            {
                disagreements.Add($"{opening} game={gameShowsAll} room={roomViewPrivate} reader={reader.UserId}: " +
                                  $"listed {listed}, shown {shown}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    private static readonly Guid GorimCharacterId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    /// <summary>The mark of a file chosen for the addressee, as the upload path freezes it.</summary>
    private static readonly string ForGorim =
        $"[{{\"character\":\"{GorimCharacterId}\",\"name\":\"Горим\",\"owner\":\"{AddresseeId}\"}}]";

    private const string ForNobody = "[]";

    private const string PlainText = "Он разложил карту на столе.";

    private static IAuthorizationSubject ModeratorOutsideTheGame() =>
        new AuthenticatedUser { UserId = Guid.NewGuid(), Username = "moderator", Role = UserRole.Moderator };

    [Fact]
    public void GiveAFileMarkedForACharacterToWhomAHiddenBlockForHimIsRead()
    {
        var post = PostWith(PlainText, mapMark: ForGorim);

        Gets(Reader(AuthorId), post, MapId).Should().BeTrue("the author reads everything in his post");
        Gets(Reader(AddresseeId), post, MapId).Should().BeTrue("the owner of the character chosen");
        Gets(Reader(MasterId), post, MapId).Should().BeTrue("the leads read every hidden block");
        Gets(Reader(AssistantId), post, MapId).Should().BeTrue();
        Gets(Reader(SecondAddresseeId), post, MapId).Should().BeFalse("another player was not chosen");
        Gets(Reader(ReaderId), post, MapId).Should().BeFalse("a reader of the room was not chosen");
        Gets(null, post, MapId).Should().BeFalse("nor was a guest");
        Gets(ModeratorOutsideTheGame(), post, MapId).Should().BeFalse("moderation has no way around a hidden block");
        Gets(Reader(ReaderId), post, LetterId).Should().BeTrue("the other file is for everybody");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GiveAMarkedFileToTheRoomWhereASettingOpensHiddenBlocks(bool gameShowsAll, bool roomViewPrivate)
    {
        var post = PostWith(PlainText, gameShowsAll, roomViewPrivate, mapMark: ForGorim);

        Gets(Reader(ReaderId), post, MapId).Should().BeTrue();
        Gets(null, post, MapId).Should().BeTrue("a guest who reads the room reads it too");
    }

    [Fact]
    public void KeepAFileMarkedForNobodyToTheAuthorAndTheLeadsWhateverTheSettings()
    {
        var post = PostWith(PlainText, gameShowsAll: true, roomViewPrivate: true, mapMark: ForNobody);

        Gets(Reader(ReaderId), post, MapId).Should().BeFalse("the settings share what is addressed to characters");
        Gets(Reader(AddresseeId), post, MapId).Should().BeFalse();
        Gets(Reader(AuthorId), post, MapId).Should().BeTrue();
        Gets(Reader(MasterId), post, MapId).Should().BeTrue();
    }

    // A mark that is no list of addressees is read as one for nobody: closed, not open.
    [Theory]
    [InlineData("{}")]
    [InlineData("не json")]
    public void ReadAMalformedMarkAsAFileForNobody(string mark)
    {
        var post = PostWith(PlainText, mapMark: mark);

        Gets(Reader(ReaderId), post, MapId).Should().BeFalse();
        Gets(Reader(MasterId), post, MapId).Should().BeTrue();
    }

    /// <summary>
    /// The text stays a guard beside the mark: a file for everybody shown only in a block for the addressee is
    /// still kept from a reader the block is not for, as before the mark existed.
    /// </summary>
    [Fact]
    public void KeepTheTextAsAGuardBesideTheMark() =>
        Gets(Reader(ReaderId), PostWith(MapForGorim), MapId).Should().BeFalse();

    // A picture of a marked file outside any block is gone for a reader the file is not for, with
    // the line it stood on: the page says nothing of a file that is not his.
    [Fact]
    public void CutThePictureOfAMarkedFileOutOfTheTextWithoutTrace()
    {
        var withPicture = PostWith($"Карта:\n{Image(MapId)}\nСвернул ее.", mapMark: ForGorim);
        var without = PostWith("Карта:\nСвернул ее.", mapMark: ForGorim);

        Rendered(MapperFor(Reader(ReaderId)).ToPost(withPicture).GameText, Reader(ReaderId))
            .Should().Be(Rendered(MapperFor(Reader(ReaderId)).ToPost(without).GameText, Reader(ReaderId)));
        Rendered(MapperFor(Reader(AddresseeId)).ToPost(withPicture).GameText, Reader(AddresseeId))
            .Should().Contain(MapId.ToString("D"));
    }

    /// <summary>The metagame text cuts the picture out as the game text does.</summary>
    [Fact]
    public void CutThePictureOfAMarkedFileOutOfTheMetagameTextToo()
    {
        var post = PostWith(PlainText, mapMark: ForGorim);
        post.MetagameText = $"Смотрите: {Image(MapId)}";

        RenderedMeta(MapperFor(Reader(ReaderId)).ToPost(post).MetagameText, Reader(ReaderId))
            .Should().NotContain(MapId.ToString("D"));
        RenderedMeta(MapperFor(Reader(AddresseeId)).ToPost(post).MetagameText, Reader(AddresseeId))
            .Should().Contain(MapId.ToString("D"));
    }

    /// <summary>
    /// For a marked file, as for one in a block: whatever the reader and the settings, the strip lists it
    /// exactly where the text shows its picture.
    /// </summary>
    [Fact]
    public void ListAMarkedFileExactlyWhereThePageShowsIt()
    {
        IAuthorizationSubject[] readers =
        [
            Reader(AuthorId), Reader(MasterId), Reader(AssistantId), Reader(AddresseeId),
            Reader(SecondAddresseeId), Reader(ReaderId), AuthenticatedUser.Guest, ModeratorOutsideTheGame()
        ];
        var disagreements = new List<string>();

        var cases =
            from mark in new[] { ForGorim, ForNobody }
            from overrides in new[] { (Game: false, Room: false), (Game: true, Room: false), (Game: false, Room: true) }
            from reader in readers
            select (mark, overrides.Game, overrides.Room, reader);

        foreach (var (mark, gameShowsAll, roomViewPrivate, reader) in cases)
        {
            var post = PostWith($"Карта: {Image(MapId)}", gameShowsAll, roomViewPrivate, mark);
            var mapped = MapperFor(reader).ToPost(post);

            var listed = mapped.Attachments.Any(a => a.Id == MapId);
            var shown = Rendered(mapped.GameText, reader).Contains(MapId.ToString("D"), StringComparison.OrdinalIgnoreCase);
            if (listed != shown)
            {
                disagreements.Add($"{mark} game={gameShowsAll} room={roomViewPrivate} reader={reader.UserId}: " +
                                  $"listed {listed}, shown {shown}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    /// <summary>
    /// Who sees a marked file goes out with it, leads first, as the recipients of a block, and its
    /// characters by the names the mark froze, for the boxes of the author.
    /// </summary>
    [Fact]
    public void NameTheRecipientsOfAMarkedFileAsOfABlock()
    {
        var attachments = MapperFor(Reader(AddresseeId)).ToPost(PostWith(PlainText, mapMark: ForGorim)).Attachments
            .ToDictionary(a => a.Id);

        attachments[MapId].Recipients.Should().Be("DungeonMaster, Assistant, Горим");
        attachments[MapId].Addressees.Should().BeEquivalentTo(
            new[] { new UploadAddressee { Id = GorimCharacterId, Name = "Горим" } });
        attachments[LetterId].Recipients.Should().BeNull("a file for everybody carries no mark");
        attachments[LetterId].Addressees.Should().BeNull();
    }

    /// <summary>The metagame text as the response carries it to the reader.</summary>
    private static string RenderedMeta(CommonBbText text, IAuthorizationSubject reader) => Serialize(text, reader);

    /// <summary>The game text as the response carries it to the reader: through the converter of the page.</summary>
    private static string Rendered(PostBbText text, IAuthorizationSubject reader) => Serialize(text, reader);

    private static string Serialize<TText>(TText text, IAuthorizationSubject reader)
    {
        var readers = Mock<IAuthorizationContextProvider>();
        readers.CurrentSubject.Returns(reader);
        var services = new ServiceCollection().AddSingleton(readers).BuildServiceProvider();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } };
        var options = new JsonOptions();
        options.Setup(accessor, new BbParserProvider());

        return JsonSerializer.Serialize(text, options.JsonSerializerOptions);
    }

    [Fact]
    public void LeaveSuchAFileOutOfThePostForAReaderTheBlockIsNotAddressedTo() =>
        MapperFor(Reader(ReaderId)).ToPost(PostWith(MapForGorim)).Attachments
            .Select(a => a.Id).Should().Equal(LetterId);

    [Fact]
    public void KeepSuchAFileInThePostForItsAddressee() =>
        MapperFor(Reader(AddresseeId)).ToPost(PostWith(MapForGorim)).Attachments
            .Select(a => a.Id).Should().Equal(MapId, LetterId);

    private static PostMapper MapperFor(IAuthorizationSubject reader)
    {
        var readers = Mock<IAuthorizationContextProvider>();
        readers.CurrentSubject.Returns(reader);

        var userMapper = new UserMapper();
        return new PostMapper(
            new CharacterMapper(userMapper),
            new GameMapper(new AttributeSchemaMapper(), Mock<IIdentityProvider>()),
            Rule,
            readers);
    }
}
