using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using Microsoft.EntityFrameworkCore;
using AwesomeAssertions;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A file attached to a post in a closed room is reachable by exactly the people
/// who may read the post, and by nobody else.
/// </summary>
/// <remarks>
/// This is the reason the feature has an endpoint of its own instead of a link.
/// The bucket answers anonymous reads on the avatar prefixes and there is no
/// signed address for anything else, so the only address an attachment has is
/// the one asserted here — and it decides on every request rather than once,
/// when it was handed out. A link that works because it is known would take the
/// file out of the private room permanently, in the hands of whoever it reached.
///
/// The cases below are chosen so that each one passes or fails for a different
/// reason: the anonymous caller has no identity, the outsider has one and no
/// standing in the game, the player has standing in the game and none in the
/// room, and the reader of the room has neither ownership of the file nor a
/// moderator's rank, so a 200 for them can only come from the room rule.
///
/// Every request goes to the address the API itself puts in the post payload —
/// asserted, not assumed, by the last test here. A suite that called the service
/// directly would prove something about a method and nothing about the link the
/// browser is given.
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

    private const string ObjectKey = "posts/f100_attachment.png";
    private static readonly byte[] FileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];

    /// <summary>Master of the game: reads every room of it.</summary>
    private static GeneralUser Master => User(
        TestConstants.SecondUserId, TestConstants.SecondUserUsername, UserRole.RegularUser);

    /// <summary>Author of the post and owner of the file.</summary>
    private static GeneralUser Author => User(
        TestConstants.TestUserId, TestConstants.TestUserUsername, UserRole.RegularUser);

    /// <summary>
    /// In the private room by a character of theirs, and neither the owner of the
    /// file nor a moderator — so a 200 for them is the room rule and nothing else.
    /// </summary>
    private static GeneralUser RoomMember => User(
        TestConstants.TestimonialUserIds[0], TestConstants.TestimonialUsernames[0], UserRole.RegularUser);

    /// <summary>In the game with a character, with no access to this room.</summary>
    private static GeneralUser PlayerWithoutTheRoom => User(
        TestConstants.TestimonialUserIds[1], TestConstants.TestimonialUsernames[1], UserRole.RegularUser);

    /// <summary>Signed in, and nothing to do with the game.</summary>
    private static GeneralUser Outsider => User(
        TestConstants.TestimonialUserIds[2], TestConstants.TestimonialUsernames[2], UserRole.RegularUser);

    private static GeneralUser Moderator => User(
        TestConstants.ModeratorUserId, TestConstants.ModeratorUserUsername, UserRole.Moderator);

    private static GeneralUser User(Guid id, string username, UserRole role) =>
        new() { UserId = id, Username = username, Role = role };

    private static string ContentUrl => $"/v1/uploads/{UploadId:D}/content";

    /// <summary>
    /// A game anybody may see, holding a room only its members may enter. The
    /// game is approved and active on purpose: the only thing closing the room is
    /// its own access type, so a refusal below cannot be the game being invisible.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        // Seeded before the guard below returns. The object store is a static
        // dictionary that outlives one run while the database does not, so the
        // two are only in step by coincidence today - and the tests that expect
        // the bytes fail with a 404 the moment they fall out of step.
        InMemoryObjectStorage.Seed(ObjectKey, FileBytes, "image/png");

        await using var db = DatabaseFixture.CreateDbContext();

        if (await db.Set<DbGame>().AnyAsync(g => g.GameId == GameId))
        {
            return;
        }

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

        db.Set<DbUpload>().Add(new DbUpload
        {
            UploadId = UploadId,
            UserId = Author.UserId,
            Type = UploadType.PostAttachment,
            Status = UploadStatus.Confirmed,
            TargetPostId = PostId,
            Original = true,
            ContentType = "image/png",
            SizeBytes = FileBytes.LongLength,
            Width = 1600,
            Height = 1200,
            ObjectKey = ObjectKey,
            // Null by construction for this type: the bucket answers nothing on
            // the post prefix, so there is no public address to record.
            FilePath = null,
            FileName = "karta.png",
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            ConfirmedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            IsRemoved = false,
        });

        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Nothing in the body is an address the bytes can be fetched from without
    /// this API deciding who is asking.
    /// </summary>
    /// <remarks>
    /// Three shapes, because there are three ways it leaks. The key itself is
    /// what the bucket answers to. The camelCased field name catches a DTO that
    /// grew the column even where the value happened to be empty. And an imgproxy
    /// address hides the key inside base64url, where neither of the first two
    /// would see it — that one is caught by the resize option, which every
    /// thumbnail URL this project builds carries in clear text ahead of the
    /// encoded source.
    /// </remarks>
    private static void AssertNoAddressOfTheBytes(string body)
    {
        body.Should().NotContain(ObjectKey, "the object key is the address the bucket answers to");
        body.Should().NotContain("objectKey", "the key must not reach any DTO at all");
        body.Should().NotContain("rs:fill:", "an imgproxy link has no expiry and names nobody");
    }

    private static DbCharacter Character(Guid id, Guid authorId, string name) => new()
    {
        CharacterId = id,
        GameId = GameId,
        AuthorId = authorId,
        Name = name,
        Status = CharacterStatus.Active,
        IsNpc = false,
        AccessPolicy = CharacterAccessPolicy.NoAccess,
        CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
        IsRemoved = false,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Who gets the bytes
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The two refusals say the same words, not merely carry the same status.
    /// </summary>
    /// <remarks>
    /// The message travels in the body: a refusal naming the post says both that
    /// a live row exists and what it hangs on, which is the difference this
    /// endpoint exists not to expose. Asserted against a genuinely absent id, so
    /// the pair is compared rather than one of them pinned to a literal.
    /// </remarks>
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

    /// <summary>
    /// 404 and not 403, for all three. A 403 answers the question the closed room
    /// was keeping shut: whether there is a file here at all.
    /// </summary>
    [Fact]
    public async Task NeverAnswerForbidden_WhichWouldConfirmTheFileExists()
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
    /// A moderator sees it wherever it is. Game content is out of reach of
    /// warnings, which is a rule about judging what people write; a file the site
    /// may have to remove is one the site has to be able to look at first.
    /// </summary>
    [Fact]
    public async Task GiveTheFileToAModeratorOutsideTheGame()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, ContentUrl, Moderator));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // How the bytes are served
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Served from the site's own origin, so the response has to say it is a
    /// download and must not be sniffed into something the browser will execute.
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
        response.Headers.CacheControl.Private.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // What the payload says about the file
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The post carries the endpoint's address and nothing else about where the
    /// bytes live.
    /// </summary>
    /// <remarks>
    /// The object key and an imgproxy address are the two ways this leaks. The
    /// key is what the bucket answers to, and an imgproxy link is signed but
    /// carries no identity and no expiry, so either one, once in a payload, is a
    /// pass on the bearer's terms rather than the reader's. The check is over the
    /// raw body: a field added to the DTO reaches this test without anybody
    /// remembering to extend it.
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
}
