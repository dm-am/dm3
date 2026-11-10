using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AwesomeAssertions;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A file attached to a post in a closed room is reachable by exactly the people
/// who may read the post, and by nobody else. A file the game text shows only
/// inside a hidden block is reachable by those who read the block.
/// </summary>
/// <remarks>
/// The bucket answers anonymously only on avatar prefixes, so this endpoint is the only address an attachment
/// has, and it decides on every request. Each case fails for a different reason, and every request goes to the
/// address the API puts in the payload.
/// </remarks>
public class PostAttachmentAccessShould : IntegrationTestBase, IAsyncLifetime
{
    public PostAttachmentAccessShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static Guid GameId => Guid.Parse("00000000-0000-0000-0000-00000000f100");
    private static Guid PrivateRoomId => Guid.Parse("00000000-0000-0000-0000-00000000f101");
    private static Guid AuthorCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f102");
    private static Guid ReaderCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f103");
    private static Guid OutsiderCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f104");
    private static Guid PostId => Guid.Parse("00000000-0000-0000-0000-00000000f105");
    private static Guid UploadId => Guid.Parse("00000000-0000-0000-0000-00000000f106");
    private static Guid LetterPostId => Guid.Parse("00000000-0000-0000-0000-00000000f107");
    private static Guid LetterUploadId => Guid.Parse("00000000-0000-0000-0000-00000000f108");
    private static Guid AddresseeCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f109");

    private const string AddresseeName = "Адресат письма";

    private const string ObjectKey = "posts/f100_attachment.png";
    private const string LetterObjectKey = "posts/f100_letter.png";
    private static readonly byte[] FileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];

    private static GeneralUser Master => User(
        TestConstants.SecondUserId, TestConstants.SecondUserLogin, UserRole.RegularUser);

    private static GeneralUser Author => User(
        TestConstants.TestUserId, TestConstants.TestUserLogin, UserRole.RegularUser);

    // In the private room by a character of theirs, and neither the owner of the file nor a
    // moderator — so a 200 for them is the room rule and nothing else.
    private static GeneralUser RoomMember => User(
        TestConstants.TestimonialUserIds[0], TestConstants.TestimonialUsernames[0], UserRole.RegularUser);

    private static GeneralUser PlayerWithoutTheRoom => User(
        TestConstants.TestimonialUserIds[1], TestConstants.TestimonialUsernames[1], UserRole.RegularUser);

    private static GeneralUser Outsider => User(
        TestConstants.TestimonialUserIds[2], TestConstants.TestimonialUsernames[2], UserRole.RegularUser);

    private static GeneralUser Moderator => User(
        TestConstants.ModeratorUserId, TestConstants.ModeratorUserLogin, UserRole.Moderator);

    private static GeneralUser Addressee => User(
        TestConstants.TestimonialUserIds[3], TestConstants.TestimonialUsernames[3], UserRole.RegularUser);

    private static GeneralUser User(Guid id, string username, UserRole role) =>
        new() { UserId = id, Username = username, Role = role };

    private static string ContentUrl => $"/v1/uploads/{UploadId:D}/content";

    private static string LetterContentUrl => $"/v1/uploads/{LetterUploadId:D}/content";

    public async ValueTask InitializeAsync()
    {
        // Seeded before the guard below returns. The object store is a static
        // dictionary that outlives one run while the database does not, so the
        // two are only in step by coincidence today - and the tests that expect
        // the bytes fail with a 404 the moment they fall out of step.
        InMemoryObjectStorage.Seed(ObjectKey, FileBytes, "image/png");
        InMemoryObjectStorage.Seed(LetterObjectKey, FileBytes, "image/png");

        await using var db = DatabaseFixture.CreateDbContext();

        if (await db.Set<DbGame>().AnyAsync(g => g.GameId == GameId))
        {
            return;
        }

        AddClosedRoom(db);
        db.Set<DbUpload>().Add(Attachment(UploadId, PostId, ObjectKey, "karta.png"));
        AddLetter(db);

        await db.SaveChangesAsync();
    }

    private static void AddLetter(DmDbContext db)
    {
        db.Set<DbCharacter>().Add(Character(AddresseeCharacterId, Addressee.UserId, AddresseeName));
        db.Set<DbRoomAccess>().Add(new DbRoomAccess
        {
            AccessId = Guid.NewGuid(),
            RoomId = PrivateRoomId,
            CharacterId = AddresseeCharacterId,
            Policy = RoomAccessPolicy.ReadOnly,
        });

        db.Set<DbPost>().Add(new DbPost
        {
            PostId = LetterPostId,
            RoomId = PrivateRoomId,
            CharacterId = AuthorCharacterId,
            AuthorId = Author.UserId,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
            GameText = $"Передаю письмо.\n[private=\"{AddresseeName}\"]" +
                       $"[img]https://dm.am{LetterContentUrl}[/img][/private]",
            // The snapshot the save path freezes for the block: its one addressee.
            PrivateAddresseeSnapshotJson =
                $"{{\"{AddresseeName}\":[{{\"character\":\"{AddresseeCharacterId}\"," +
                $"\"name\":\"{AddresseeName}\",\"owner\":\"{Addressee.UserId}\"}}]}}",
            IsRemoved = false,
        });

        db.Set<DbUpload>().Add(Attachment(LetterUploadId, LetterPostId, LetterObjectKey, "pismo.png"));
    }

    // A visible game holding a room only its members may enter: only the room's access type closes
    // it.
    private static void AddClosedRoom(DmDbContext db)
    {
        db.Set<DbGame>().Add(new DbGame
        {
            GameId = GameId,
            PublicId = "gameq",
            MasterId = Master.UserId,
            Title = "Игра с приватной комнатой",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = "Проверка доступа к вложениям.",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            IsRemoved = false,
        });

        db.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = PrivateRoomId,
            GameId = GameId,
            RoomNumber = 1,
            Title = "Закрытая комната",
            AccessType = RoomAccessType.Private,
            Type = RoomType.Default,
            OrderNumber = 1.0,
            IsRemoved = false,
        });

        db.Set<DbCharacter>().AddRange(
            Character(AuthorCharacterId, Author.UserId, "Автор поста"),
            Character(ReaderCharacterId, RoomMember.UserId, "Сосед по комнате"),
            Character(OutsiderCharacterId, PlayerWithoutTheRoom.UserId, "Игрок другой комнаты"));

        // Two of the three characters are let into the room; the third is in the
        // game and stops at its door.
        db.Set<DbRoomAccess>().AddRange(
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = PrivateRoomId,
                CharacterId = AuthorCharacterId,
                Policy = RoomAccessPolicy.Full,
            },
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = PrivateRoomId,
                CharacterId = ReaderCharacterId,
                Policy = RoomAccessPolicy.ReadOnly,
            });

        db.Set<DbPost>().Add(new DbPost
        {
            PostId = PostId,
            RoomId = PrivateRoomId,
            CharacterId = AuthorCharacterId,
            AuthorId = Author.UserId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            GameText = "Разворачиваю карту на столе.",
            PrivateAddresseeSnapshotJson = "{}",
            IsRemoved = false,
        });
    }

    private static DbUpload Attachment(Guid uploadId, Guid postId, string objectKey, string fileName) => new()
    {
        UploadId = uploadId,
        UserId = Author.UserId,
        Type = UploadType.PostAttachment,
        Status = UploadStatus.Confirmed,
        TargetPostId = postId,
        Original = true,
        ContentType = "image/png",
        SizeBytes = FileBytes.LongLength,
        Width = 1600,
        Height = 1200,
        ObjectKey = objectKey,
        // Null by construction for this type: the bucket answers nothing on
        // the post prefix, so there is no public address to record.
        FilePath = null,
        FileName = fileName,
        CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
        ConfirmedUtc = DateTimeOffset.UtcNow.AddHours(-1),
        IsRemoved = false,
    };

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Nothing in the body is an address the bytes can be fetched from without
    /// this API deciding who is asking.
    /// </summary>
    private static void AssertNoAddressOfTheBytes(string body)
    {
        body.Should().NotContain(Path.ChangeExtension(ObjectKey, null),
            "the object key is the address the bucket answers to, and a thumbnail lies beside it");
        body.Should().NotContain("objectKey", "the key must not reach any DTO at all");
    }

    private static DbCharacter Character(Guid id, Guid authorId, string name) =>
        CharacterRows.InPlay(id, GameId, authorId, name);

    /// <remarks>A refusal naming the post would say a live row exists; compared against a genuinely absent id.</remarks>
    [Fact]
    public async Task AnswerAClosedFileWithTheSameWordsAsAMissingOne()
    {
        var closed = await Client.GetAsync(ContentUrl);
        var missing = await Client.GetAsync($"/v1/uploads/{Guid.NewGuid()}/content");

        closed.StatusCode.Should().Be(missing.StatusCode);

        var closedTitle = await TitleOf(closed);
        var missingTitle = await TitleOf(missing);

        closedTitle.Should().Be(missingTitle,
            "an id whose answer differs between \"closed to you\" and \"never existed\" " +
            "tells the holder which of the two it is");
        closedTitle.Should().NotContain("ост",
            "naming the post names what the file hangs on, and that is half the answer");
    }

    private static async Task<string?> TitleOf(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.TryGetProperty("title", out var title) ? title.GetString() : null;
    }

    [Fact]
    public async Task RefuseAnAnonymousCaller()
    {
        var response = await Client.GetAsync(ContentUrl);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "an address that opens a private room's file to anyone holding it is the leak this endpoint exists to close");
    }

    [Fact]
    public async Task RefuseASignedInOutsider()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, Outsider));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RefuseAPlayerOfTheGameWhoIsNotInThisRoom()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, PlayerWithoutTheRoom));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the room is the unit of access, not the game");
    }

    // A 403 answers the question the closed room was keeping shut: whether there is a file here at
    // all.
    [Fact]
    public async Task NeverAnswerForbiddenWhichWouldConfirmTheFileExists()
    {
        foreach (var request in new[]
                 {
                     new HttpRequestMessage(HttpMethod.Get, ContentUrl),
                     CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, Outsider),
                     CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, PlayerWithoutTheRoom),
                 })
        {
            var response = await Client.SendAsync(request);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task GiveTheFileToAReaderOfTheRoom()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, RoomMember));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(FileBytes);
    }

    [Fact]
    public async Task GiveTheFileToTheAuthorAndToTheMaster()
    {
        foreach (var user in new[] { Author, Master })
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, user));

            response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} may read the post", user.Username);
        }
    }

    /// <summary>
    /// A moderator sees it wherever the post is: a file the site may have to remove it has to be able to look at.
    /// A file inside a hidden block is the exception, see KeepSuchAFileFromAModeratorWhomTheUploadRecordGivesItsAddress.
    /// </summary>
    [Fact]
    public async Task GiveTheFileToAModeratorOutsideTheGame()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, Moderator));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Served from the site's own origin, so the response has to say it is a
    /// download and must not be sniffed into something the browser will execute.
    /// A file of a closed room is not kept even by the browser: the next account
    /// signed in there would be served it from the copy.
    /// </summary>
    [Fact]
    public async Task ServeTheFileAsADownloadThatIsNeverSniffedOrSharedByACache()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, Author));

        response.Content.Headers.ContentDisposition!.DispositionType
            .Should().Be("attachment");
        response.Headers.GetValues("X-Content-Type-Options")
            .Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.CacheControl!.Public.Should().BeFalse(
            "who may read this was decided for one caller, so a shared cache must not answer the next one");
        response.Headers.CacheControl.NoStore.Should().BeTrue(
            "a guest does not read a closed room, so no copy may outlive this answer");
        response.Headers.CacheControl.MaxAge.Should().BeNull();
    }

    /// <remarks>
    /// The key and a thumbnail beside it are both passes on the bearer's terms. Checked over the raw body, so a
    /// new DTO field is covered.
    /// </remarks>
    [Fact]
    public async Task NameTheContentEndpointAndNeitherTheObjectKeyNorAThumbnailService()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/posts/{PostId:D}", Author));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        AssertNoAddressOfTheBytes(body);

        using var document = JsonDocument.Parse(body);
        var attachment = document.RootElement
            .GetProperty("resource").GetProperty("attachments").EnumerateArray().Single();

        attachment.GetProperty("id").GetGuid().Should().Be(UploadId);
        attachment.GetProperty("url").GetString().Should().Be(ContentUrl);
        attachment.GetProperty("fileName").GetString().Should().Be("karta.png");
        attachment.GetProperty("sizeBytes").GetInt64().Should().Be(FileBytes.LongLength);
        attachment.GetProperty("width").GetInt32().Should().Be(1600);
        attachment.GetProperty("height").GetInt32().Should().Be(1200);
    }

    /// <summary>
    /// The paged read of the room says the same thing as the single-post read.
    /// </summary>
    [Fact]
    public async Task CarryAttachmentsOnThePagedReadOfTheRoomToo()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/rooms/{PrivateRoomId:D}/posts", RoomMember));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        AssertNoAddressOfTheBytes(body);

        using var document = JsonDocument.Parse(body);
        var post = document.RootElement.GetProperty("resources").EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == PostId);
        post.GetProperty("attachments").EnumerateArray().Single()
            .GetProperty("url").GetString().Should().Be(ContentUrl);
    }

    /// <summary>
    /// A reader of the room the block of the letter is not addressed to: the file is
    /// as absent to him as one that never existed, and is answered in the same words.
    /// </summary>
    [Fact]
    public async Task AnswerAFileShownOnlyInABlockNotAddressedToTheReaderAsAMissingOne()
    {
        var hidden = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, LetterContentUrl, RoomMember));
        var missing = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/uploads/{Guid.NewGuid()}/content", RoomMember));

        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a picture the block keeps from him is no more his than the text around it");
        (await TitleOf(hidden)).Should().Be(await TitleOf(missing));
    }

    // Nor does the post list it for him, read alone or with the room: a file under the post would
    // say what the block keeps from him.
    [Fact]
    public async Task LeaveSuchAFileOutOfThePostForThatReader()
    {
        (await AttachmentIdsOf(LetterPostId, RoomMember)).Should().BeEmpty();

        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/rooms/{PrivateRoomId:D}/posts", RoomMember));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var letter = document.RootElement.GetProperty("resources").EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == LetterPostId);
        IdsOf(letter).Should().BeEmpty();
    }

    [Fact]
    public async Task GiveSuchAFileToTheAddresseeTheAuthorAndTheMaster()
    {
        foreach (var user in new[] { Addressee, Author, Master })
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, LetterContentUrl, user));

            response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} reads the block", user.Username);
            (await AttachmentIdsOf(LetterPostId, user)).Should().Equal(
                new[] { LetterUploadId }, "{0} reads the block", user.Username);
        }
    }

    // Moderation has no way around a hidden block, and the address of a file is none either: the
    // upload record hands a moderator outside the game the address, and the address answers him as
    // a missing file.
    [Fact]
    public async Task KeepSuchAFileFromAModeratorWhomTheUploadRecordGivesItsAddress()
    {
        var record = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/uploads/{LetterUploadId:D}", Moderator));
        record.StatusCode.Should().Be(HttpStatusCode.OK, "a moderator reads the record of any upload");

        var payload = await record.Content.ReadFromJsonAsync<JsonElement>();
        var upload = payload.TryGetProperty("resource", out var resource) ? resource : payload;
        var contentUrl = upload.GetProperty("contentUrl").GetString();
        contentUrl.Should().Be(LetterContentUrl);

        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, contentUrl!, Moderator));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the block is not his to read, and the file goes with the block");
    }

    /// <summary>The attachments of a post as the reader gets it alone.</summary>
    private async Task<Guid[]> AttachmentIdsOf(Guid postId, GeneralUser reader)
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/posts/{postId:D}", reader));
        response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} reads the post", reader.Username);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return IdsOf(document.RootElement.GetProperty("resource"));
    }

    private static Guid[] IdsOf(JsonElement post) =>
        post.TryGetProperty("attachments", out var attachments)
            ? attachments.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToArray()
            : [];
}
