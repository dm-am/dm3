using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
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
/// A file of a post marked "Выбранные" is read by exactly those who would read a hidden block with the
/// same addressees in that post, through every door: the strip under the post, the picture in its text,
/// the address of the file and the record of the upload.
/// </summary>
/// <remarks>
/// One closed room: the author, one player chosen and another not, the master leading the game, and a character
/// of the game no longer let in. Every refusal is compared against a file that does not exist.
/// </remarks>
public class PostAttachmentAudienceShould : IntegrationTestBase, IAsyncLifetime
{
    public PostAttachmentAudienceShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static Guid GameId => Guid.Parse("00000000-0000-0000-0000-00000000f200");
    private static Guid RoomId => Guid.Parse("00000000-0000-0000-0000-00000000f201");
    private static Guid AuthorCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f202");
    private static Guid VasyaCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f203");
    private static Guid PetyaCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f204");
    private static Guid PostId => Guid.Parse("00000000-0000-0000-0000-00000000f205");
    private static Guid UploadPostId => Guid.Parse("00000000-0000-0000-0000-00000000f206");

    private static Guid GoneCharacterId => Guid.Parse("00000000-0000-0000-0000-00000000f207");

    // The post carrying DepartedId, so the strip of PostId stays as it is.
    private static Guid DepartedPostId => Guid.Parse("00000000-0000-0000-0000-00000000f208");

    private static Guid MarkedId => Guid.Parse("00000000-0000-0000-0000-00000000f210");

    private static Guid NobodyId => Guid.Parse("00000000-0000-0000-0000-00000000f211");

    private static Guid OpenId => Guid.Parse("00000000-0000-0000-0000-00000000f212");

    private static Guid LetterId => Guid.Parse("00000000-0000-0000-0000-00000000f213");

    // For everybody, its mark changed by the cases that change one.
    private static Guid SwitchId => Guid.Parse("00000000-0000-0000-0000-00000000f214");

    private static Guid DepartedId => Guid.Parse("00000000-0000-0000-0000-00000000f215");

    private static readonly byte[] FileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x03, 0x04];

    private static GeneralUser Master => User(TestConstants.SecondUserId, TestConstants.SecondUserLogin);

    private static GeneralUser Author => User(TestConstants.TestUserId, TestConstants.TestUserLogin);

    private static GeneralUser Vasya => User(TestConstants.TestimonialUserIds[4], TestConstants.TestimonialUsernames[4]);

    private static GeneralUser Petya => User(TestConstants.TestimonialUserIds[5], TestConstants.TestimonialUsernames[5]);

    private static GeneralUser Outsider =>
        User(TestConstants.TestimonialUserIds[6], TestConstants.TestimonialUsernames[6]);

    private static GeneralUser GoneOwner =>
        User(TestConstants.TestimonialUserIds[7], TestConstants.TestimonialUsernames[7]);

    private static GeneralUser Moderator =>
        User(TestConstants.ModeratorUserId, TestConstants.ModeratorUserLogin, UserRole.Moderator);

    private static GeneralUser User(Guid id, string username, UserRole role = UserRole.RegularUser) =>
        new() { UserId = id, Username = username, Role = role };

    private static string Address(Guid uploadId) => $"/v1/uploads/{uploadId:D}/content";

    private static string MarkFor(params (Guid Character, string Name, Guid Owner)[] characters) =>
        "[" + string.Join(",", characters.Select(c =>
            $"{{\"character\":\"{c.Character}\",\"name\":\"{c.Name}\",\"owner\":\"{c.Owner}\"}}")) + "]";

    private static string ForVasya => MarkFor((VasyaCharacterId, "Вася", Vasya.UserId));

    // The mark of DepartedId, frozen while both were let into the room.
    private static string ForVasyaAndTheGone =>
        MarkFor((VasyaCharacterId, "Вася", Vasya.UserId), (GoneCharacterId, "Ушедший", GoneOwner.UserId));

    private static string GameText =>
        $"Карта: [img]https://dm.am{Address(MarkedId)}[/img]\n" +
        $"[private=\"Вася\"]Письмо: [img]https://dm.am{Address(LetterId)}[/img][/private]";

    public async ValueTask InitializeAsync()
    {
        foreach (var id in new[] { MarkedId, NobodyId, OpenId, LetterId, SwitchId, DepartedId })
        {
            InMemoryObjectStorage.Seed(ObjectKey(id), FileBytes, "image/png");
        }

        await using var db = DatabaseFixture.CreateDbContext();
        if (await db.Set<DbGame>().AnyAsync(g => g.GameId == GameId))
        {
            return;
        }

        AddRoom(db);
        db.Set<DbUpload>().AddRange(
            Attachment(MarkedId, "karta.png", ForVasya),
            Attachment(NobodyId, "zametka.png", "[]"),
            Attachment(OpenId, "obshchee.png", null),
            Attachment(LetterId, "pismo.png", ForVasya),
            Attachment(SwitchId, "pereklyuchaemoe.png", null),
            Attachment(DepartedId, "dlya-dvoih.png", ForVasyaAndTheGone, DepartedPostId));
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string ObjectKey(Guid id) => $"posts/{id:N}.png";

    private static void AddRoom(DmDbContext db)
    {
        db.Set<DbGame>().Add(new DbGame
        {
            GameId = GameId,
            PublicId = "gamew",
            MasterId = Master.UserId,
            Title = "Игра с отмеченными файлами",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = "Проверка того, кто видит файлы поста.",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-3),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
        });
        db.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = RoomId,
            GameId = GameId,
            RoomNumber = 1,
            Title = "Закрытая комната",
            AccessType = RoomAccessType.Private,
            Type = RoomType.Default,
            OrderNumber = 1.0,
        });
        db.Set<DbCharacter>().AddRange(
            Character(AuthorCharacterId, Author.UserId, "Автор"),
            Character(VasyaCharacterId, Vasya.UserId, "Вася"),
            Character(PetyaCharacterId, Petya.UserId, "Петя"),
            Character(GoneCharacterId, GoneOwner.UserId, "Ушедший"));
        foreach (var characterId in new[] { AuthorCharacterId, VasyaCharacterId, PetyaCharacterId })
        {
            db.Set<DbRoomAccess>().Add(new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = RoomId,
                CharacterId = characterId,
                Policy = RoomAccessPolicy.Full,
            });
        }

        foreach (var (postId, text) in new[]
                 {
                     (PostId, GameText), (UploadPostId, "Пост без файлов."), (DepartedPostId, "Пост с файлом для двоих."),
                 })
        {
            db.Set<DbPost>().Add(new DbPost
            {
                PostId = postId,
                RoomId = RoomId,
                CharacterId = AuthorCharacterId,
                AuthorId = Author.UserId,
                CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
                GameText = text,
                PrivateAddresseeSnapshotJson = $"{{\"Вася\":{ForVasya}}}",
            });
        }
    }

    private static DbCharacter Character(Guid id, Guid authorId, string name) =>
        CharacterRows.InPlay(id, GameId, authorId, name);

    private static DbUpload Attachment(Guid id, string fileName, string? addresseesJson, Guid? postId = null) => new()
    {
        UploadId = id,
        UserId = Author.UserId,
        Type = UploadType.PostAttachment,
        Status = UploadStatus.Confirmed,
        TargetPostId = postId ?? PostId,
        Original = true,
        ContentType = "image/png",
        SizeBytes = FileBytes.LongLength,
        ObjectKey = ObjectKey(id),
        FileName = fileName,
        AddresseesJson = addresseesJson,
        CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-50),
        ConfirmedUtc = DateTimeOffset.UtcNow.AddMinutes(-50),
    };

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, GeneralUser? user, object? body = null)
    {
        var request = user == null ? new HttpRequestMessage(method, url) : CreateAuthenticatedRequest(method, url, user);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await Client.SendAsync(request);
    }

    private static async Task<string?> TitleOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("title", out var title)
            ? title.GetString()
            : null;

    private async Task<JsonElement> PostAs(GeneralUser user, Guid? postId = null)
    {
        var response = await Send(HttpMethod.Get, $"/v1/posts/{postId ?? PostId:D}", user);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} reads the post", user.Username);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").Clone();
    }

    // The recipients a file goes out with; a null field is left out of the body.
    private static string? Recipients(JsonElement file) =>
        file.TryGetProperty("recipients", out var recipients) ? recipients.GetString() : null;

    private static (Guid Id, string? Name)[]? Addressees(JsonElement file) =>
        file.TryGetProperty("addressees", out var addressees)
            ? addressees.EnumerateArray().Select(a => (a.GetProperty("id").GetGuid(), a.GetProperty("name").GetString()))
                .ToArray()
            : null;

    private static Guid[] AttachmentIds(JsonElement post) =>
        post.GetProperty("attachments").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task AnswerAMarkedFileToAReaderNotChosenAsAMissingOne()
    {
        var missing = await TitleOf(await Send(HttpMethod.Get, Address(Guid.NewGuid()), Petya));

        foreach (var user in new[] { Petya, Moderator, Outsider })
        {
            var response = await Send(HttpMethod.Get, Address(MarkedId), user);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was not chosen", user.Username);
            (await TitleOf(response)).Should().Be(missing);
        }

        (await Send(HttpMethod.Get, Address(MarkedId), null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GiveAMarkedFileToTheChosenTheAuthorAndTheMaster()
    {
        foreach (var user in new[] { Vasya, Author, Master })
        {
            (await Send(HttpMethod.Get, Address(MarkedId), user)).StatusCode
                .Should().Be(HttpStatusCode.OK, "{0} reads a block for Вася", user.Username);
        }

        (await Send(HttpMethod.Get, Address(NobodyId), Vasya)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a file for nobody is the author's and the leads'");
        (await Send(HttpMethod.Get, Address(NobodyId), Master)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // The strip, the count at the clip and the recipients come from one list, as the reader gets
    // it.
    [Fact]
    public async Task ListAMarkedFileOnlyForWhomItIsReadWithItsRecipients()
    {
        AttachmentIds(await PostAs(Petya)).Should().BeEquivalentTo(new[] { OpenId, SwitchId });

        var forVasya = (await PostAs(Vasya)).GetProperty("attachments").EnumerateArray()
            .ToDictionary(a => a.GetProperty("id").GetGuid());
        forVasya.Keys.Should().BeEquivalentTo(new[] { MarkedId, OpenId, LetterId, SwitchId });
        forVasya[MarkedId].GetProperty("recipients").GetString().Should().Be("DungeonMaster, Assistant, Вася");
        Addressees(forVasya[MarkedId]).Should().Equal((VasyaCharacterId, "Вася"));
        Recipients(forVasya[OpenId]).Should().BeNull("a file for everybody carries no mark");
        Addressees(forVasya[OpenId]).Should().BeNull();
    }

    [Fact]
    public async Task KeepACharacterGoneFromTheRoomOnTheFileUntilTheAuthorDropsHim()
    {
        var file = (await PostAs(Author, DepartedPostId)).GetProperty("attachments").EnumerateArray().Single();
        Addressees(file).Should().Equal((VasyaCharacterId, "Вася"), (GoneCharacterId, "Ушедший"));
        Recipients(file).Should().Be("DungeonMaster, Assistant, Вася, Ушедший");

        try
        {
            var kept = await ChangeTheMarkOfTheDepartedFile(GoneCharacterId, PetyaCharacterId);
            kept.StatusCode.Should().Be(HttpStatusCode.OK, "a character the mark keeps is no stranger to the file");
            Addressees((await kept.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource"))
                .Should().BeEquivalentTo(new[] { (PetyaCharacterId, "Петя"), (GoneCharacterId, "Ушедший") });

            (await ChangeTheMarkOfTheDepartedFile(PetyaCharacterId)).StatusCode.Should().Be(HttpStatusCode.OK);
            var again = await ChangeTheMarkOfTheDepartedFile(GoneCharacterId);
            again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await TitleOf(again)).Should().Be(RefusalMessage.CharacterNotInRoom);
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Set<DbUpload>().Where(u => u.UploadId == DepartedId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AddresseesJson, ForVasyaAndTheGone));
        }
    }

    private Task<HttpResponseMessage> ChangeTheMarkOfTheDepartedFile(params Guid[] characterIds) =>
        Send(HttpMethod.Patch, $"/v1/uploads/{DepartedId:D}", Author, new { visibleTo = "Selected", characterIds });

    [Fact]
    public async Task CutThePictureOfAMarkedFileOutOfTheTextForAReaderNotChosen()
    {
        (await PostAs(Petya)).GetProperty("gameText").GetString().Should().NotContain(MarkedId.ToString("D"));
        (await PostAs(Vasya)).GetProperty("gameText").GetString().Should().Contain(MarkedId.ToString("D"));
    }

    // The text view and the safe view cut the picture as the page does: the audience is a header
    // any caller sends, and the text of a picture is its address.
    [Theory]
    [InlineData("plain_text")]
    [InlineData("embed_safe")]
    public async Task CutThePictureOfAMarkedFileOutOfEveryViewOfTheText(string audience)
    {
        (await GameTextAs(Petya, audience)).Should().NotContain(MarkedId.ToString("D"));
        (await GameTextAs(Vasya, audience)).Should().Contain(MarkedId.ToString("D"));
    }

    private async Task<string?> GameTextAs(GeneralUser user, string audience)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/posts/{PostId:D}", user);
        request.Headers.Add(BbAudienceHeader.HeaderName, audience);
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("resource").GetProperty("gameText").GetString();
    }

    /// <summary>"Показывать всем скрытые блоки" of the game opens a file marked for a character as the block.</summary>
    [Fact]
    public async Task OpenAMarkedFileToTheRoomWhenTheGameShowsEveryHiddenBlock()
    {
        await SetGameShowsAll(true);
        try
        {
            (await Send(HttpMethod.Get, Address(MarkedId), Petya)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await Send(HttpMethod.Get, Address(NobodyId), Petya)).StatusCode.Should().Be(HttpStatusCode.NotFound,
                "the setting shares what is addressed to characters");
            (await PostAs(Petya)).GetProperty("gameText").GetString().Should().Contain("Письмо",
                "the block for Вася is shown to every reader of the game");
        }
        finally
        {
            await SetGameShowsAll(false);
        }
    }

    private async Task SetGameShowsAll(bool value)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Set<DbGame>().Where(g => g.GameId == GameId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ShowPrivateMessages, value));
    }

    [Fact]
    public async Task ChangeTheMarkForTheNextRequestByTheAuthorOnly()
    {
        var chosen = new { visibleTo = "Selected", characterIds = new[] { VasyaCharacterId } };

        (await Send(HttpMethod.Patch, $"/v1/uploads/{SwitchId:D}", Master, chosen)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "the mark is the author's");

        var foreign = await Send(HttpMethod.Patch, $"/v1/uploads/{SwitchId:D}", Author,
            new { visibleTo = "Selected", characterIds = new[] { Guid.NewGuid() } });
        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TitleOf(foreign)).Should().Be(RefusalMessage.CharacterNotInRoom);

        try
        {
            var marked = await Send(HttpMethod.Patch, $"/v1/uploads/{SwitchId:D}", Author, chosen);
            marked.StatusCode.Should().Be(HttpStatusCode.OK);
            (await marked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resource").GetProperty("recipients").GetString()
                .Should().Be("DungeonMaster, Assistant, Вася");
            (await Send(HttpMethod.Get, Address(SwitchId), Petya)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await Send(HttpMethod.Get, Address(SwitchId), Vasya)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            (await Send(HttpMethod.Patch, $"/v1/uploads/{SwitchId:D}", Author, new { visibleTo = "All" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await Send(HttpMethod.Get, Address(SwitchId), Petya)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // A change of the mark tells a caller nothing of a file he may not have, as its address does
    // not: a file of a post he cannot read and a file kept from him answer as one that does not
    // exist.
    [Fact]
    public async Task AnswerAChangeOfAFileTheCallerMayNotHaveAsOfAMissingOne()
    {
        var chosen = new { visibleTo = "Selected", characterIds = new[] { VasyaCharacterId } };
        var missing = await TitleOf(await Send(HttpMethod.Patch, $"/v1/uploads/{Guid.NewGuid():D}", Petya, chosen));

        foreach (var (user, id) in new[] { (Outsider, SwitchId), (Petya, MarkedId), (Moderator, MarkedId) })
        {
            var response = await Send(HttpMethod.Patch, $"/v1/uploads/{id:D}", user, chosen);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} may not have the file", user.Username);
            (await TitleOf(response)).Should().Be(missing);
        }

        (await Send(HttpMethod.Patch, $"/v1/uploads/{SwitchId:D}", Petya, chosen)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "he reads the file and the post, and did not write it");
    }

    // The record of a file and its removal tell a caller nothing of a file he may not have either:
    // the words of a file that does not exist, and 403 only to one who reads the file and may not
    // do it.
    [Fact]
    public async Task AnswerTheRecordAndTheRemovalOfAFileTheCallerMayNotHaveAsOfAMissingOne()
    {
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Delete })
        {
            var missing = await TitleOf(await Send(method, $"/v1/uploads/{Guid.NewGuid():D}", Petya));

            foreach (var (user, id) in new[] { (Outsider, SwitchId), (Petya, MarkedId) })
            {
                var response = await Send(method, $"/v1/uploads/{id:D}", user);
                response.StatusCode.Should().Be(HttpStatusCode.NotFound,
                    "{0} may not have the file, asked by {1}", user.Username, method);
                (await TitleOf(response)).Should().Be(missing);
            }

            (await Send(method, $"/v1/uploads/{SwitchId:D}", Petya)).StatusCode.Should().Be(
                HttpStatusCode.Forbidden, "he reads the file and the post, and neither is his, asked by {0}", method);
        }
    }

    // A change that names no choice is refused, rather than read as "Все".
    [Fact]
    public async Task RefuseAChangeOfTheMarkThatChoosesNothing()
    {
        var response = await Send(HttpMethod.Patch, $"/v1/uploads/{MarkedId:D}", Author,
            new { characterIds = new[] { VasyaCharacterId } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("visibleTo")
            .EnumerateArray().Select(e => e.GetString()).Should().Equal(RefusalMessage.AudienceNotChosen);
        (await Send(HttpMethod.Get, Address(MarkedId), Petya)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the mark stays as it was");
    }

    [Fact]
    public async Task RefuseEverybodyForAFileTheTextPutsOnlyInsideAHiddenBlock()
    {
        var toEverybody = await Send(HttpMethod.Patch, $"/v1/uploads/{LetterId:D}", Author, new { visibleTo = "All" });
        toEverybody.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TitleOf(toEverybody)).Should().Be(RefusalMessage.FileForAllOnlyInsideHiddenBlock("pismo.png"));

        var intoTheBlock = await Send(HttpMethod.Patch, $"/v1/posts/{PostId:D}", Author, new
        {
            gameText = $"{GameText} [private=\"Вася\"][img]https://dm.am{Address(OpenId)}[/img][/private]",
        });
        intoTheBlock.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TitleOf(intoTheBlock)).Should().Be(RefusalMessage.FileForAllOnlyInsideHiddenBlock("obshchee.png"));
        (await PostAs(Author)).GetProperty("gameText").GetString().Should().NotContain(OpenId.ToString("D"),
            "a refused save writes nothing");
    }

    [Fact]
    public async Task NameAMarkedFileToAModeratorWithoutOpeningIt()
    {
        var forModerator = await (await Send(HttpMethod.Get, $"/v1/uploads/{MarkedId:D}", Moderator))
            .Content.ReadFromJsonAsync<JsonElement>();
        forModerator.GetProperty("canOpen").GetBoolean().Should().BeFalse();
        Recipients(forModerator).Should().BeNull();
        Addressees(forModerator).Should().BeNull();

        var forAuthor = await (await Send(HttpMethod.Get, $"/v1/uploads/{MarkedId:D}", Author))
            .Content.ReadFromJsonAsync<JsonElement>();
        forAuthor.GetProperty("canOpen").GetBoolean().Should().BeTrue();
        forAuthor.GetProperty("recipients").GetString().Should().Be("DungeonMaster, Assistant, Вася");
        Addressees(forAuthor).Should().Equal((VasyaCharacterId, "Вася"));
    }

    // A file uploaded for the addressee is closed to another player from the first request after
    // the answer.
    [Fact]
    public async Task UploadAFileWithItsMarkInOneStep()
    {
        var uploaded = await UploadToThePostWithoutFiles(
            ("Selected", "visibleTo"), (VasyaCharacterId.ToString(), "characterIds"));

        uploaded.GetProperty("recipients").GetString().Should().Be("DungeonMaster, Assistant, Вася");
        var id = uploaded.GetProperty("id").GetGuid();

        (await Send(HttpMethod.Get, Address(id), Petya)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Send(HttpMethod.Get, Address(id), Vasya)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // The form names who sees the file and no character, as the site sends "Все" and a bare
    // "Выбранные": the upload goes through, and the other player reads the file by the mark.
    [Theory]
    [InlineData("All", HttpStatusCode.OK)]
    [InlineData("Selected", HttpStatusCode.NotFound)]
    public async Task UploadAFileWhoseMarkChoosesNoCharacter(string visibleTo, HttpStatusCode forPetya)
    {
        var uploaded = await UploadToThePostWithoutFiles((visibleTo, "visibleTo"));

        var id = uploaded.GetProperty("id").GetGuid();
        (await Send(HttpMethod.Get, Address(id), Petya)).StatusCode.Should().Be(forPetya);
        (await Send(HttpMethod.Get, Address(id), Author)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<JsonElement> UploadToThePostWithoutFiles(params (string Value, string Name)[] fields)
    {
        using var picture = new MagickImage(MagickColors.Red, 8, 8);
        var file = new ByteArrayContent(picture.ToByteArray(MagickFormat.Png));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "dlya-vasi.png" } };
        foreach (var (value, name) in fields)
        {
            form.Add(new StringContent(value), name);
        }

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/uploads?type=PostAttachment&targetId={UploadPostId:D}", Author);
        request.Content = form;

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, "body was: {0}", body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    // Who sees a file is chosen on a file of a post alone, and the schema says so.
    [Fact]
    public async Task RefuseAMarkOnAnAvatarInTheSchema()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<DbUpload>().Add(new DbUpload
        {
            UploadId = Guid.NewGuid(),
            UserId = Outsider.UserId,
            Type = UploadType.UserAvatar,
            Status = UploadStatus.Confirmed,
            TargetUserId = Outsider.UserId,
            ContentType = "image/png",
            ObjectKey = $"avatars/{Guid.NewGuid():N}.png",
            AddresseesJson = "[]",
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.InnerException!.Message
            .Should().Contain("CK_Uploads_AddresseesOnPostAttachment");
    }
}
