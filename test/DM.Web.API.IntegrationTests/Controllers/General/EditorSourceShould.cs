using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Whoever may edit a text gets it for editing as it was handed in, as its author
/// does, and a reader without that right gets the render.
/// </summary>
/// <remarks>
/// The editor saves the string it was given: a render handed to a moderator or a
/// master would be written back into the text as HTML. The right is the one the
/// write of the text checks.
/// </remarks>
public class EditorSourceShould : IntegrationTestBase
{
    private const string Source = "[b]Жирный[/b] текст";

    public EditorSourceShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>A reader with no right to edit either text: a mentor outside the game and the board staff.</summary>
    private static GeneralUser Reader => new()
    {
        UserId = TestConstants.MentorUserId,
        Username = TestConstants.MentorUserLogin,
        Role = UserRole.Mentor,
        AccessPolicy = AccessPolicy.NotSpecified
    };

    [Fact]
    public async Task HandAModeratorTheSourceOfSomebodyElsesTopic()
    {
        var topicId = await SeedTopic();

        try
        {
            (await Text($"/v1/topics/{topicId}", "description", CustomWebApplicationFactory.CreateModeratorUser()))
                .Should().Be(Source);
            (await Text($"/v1/topics/{topicId}", "description", Reader))
                .Should().Contain("<strong>").And.NotContain("[b]");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Set<Topic>().Where(t => t.TopicId == topicId).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// The master edits a player's post written without a character, though not its
    /// author. Moderation does not: nothing in a game room is moderated.
    /// </summary>
    [Fact]
    public async Task HandTheMasterAndNotAModeratorTheSourceOfAPlayersPost()
    {
        var postId = await SeedPost(Source);

        try
        {
            (await Text($"/v1/posts/{postId:D}", "gameText", CustomWebApplicationFactory.CreateTestUser()))
                .Should().Be(Source, "the master of the game edits the post");
            (await Text($"/v1/posts/{postId:D}", "gameText", CustomWebApplicationFactory.CreateModeratorUser()))
                .Should().Contain("<strong>").And.NotContain("[b]", "moderation does not edit a post in a room");
            (await Text($"/v1/posts/{postId:D}", "gameText", Reader))
                .Should().Contain("<strong>").And.NotContain("[b]");
        }
        finally
        {
            await DeletePost(postId);
        }
    }

    /// <summary>
    /// The right of moderation to edit a post is not a right to read its hidden
    /// blocks: the source would hand them over, and the editor would save the post
    /// without them. Such a post is not offered for editing, is not taken edited,
    /// and its source is not handed out. The master reads them all.
    /// </summary>
    [Fact]
    public async Task KeepTheHiddenBlocksOfAPostFromAModerator()
    {
        const string withBlock = "[private=\"Кто-то\"]тайна[/private] ход";
        var postId = await SeedPost(withBlock);

        try
        {
            await ExpectTheEditKeptFromAModerator(postId, withBlock, "тайна");
        }
        finally
        {
            await DeletePost(postId);
        }
    }

    /// <summary>
    /// The moderator gets the render without <paramref name="kept"/>, no "Править" and a refused
    /// edit; the master gets the source and the right.
    /// </summary>
    private async Task ExpectTheEditKeptFromAModerator(Guid postId, string source, string kept)
    {
        var moderator = CustomWebApplicationFactory.CreateModeratorUser();
        var master = CustomWebApplicationFactory.CreateTestUser();

        (await Text($"/v1/posts/{postId:D}", "gameText", moderator)).Should().NotContain(kept);
        (await Text($"/v1/posts/{postId:D}", "gameText", master)).Should().Be(source);
        (await CanEdit(postId, moderator)).Should().BeFalse();
        (await CanEdit(postId, master)).Should().BeTrue();

        var edit = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/posts/{postId:D}", moderator);
        edit.Content = JsonContent.Create(new { gameText = "ход" });
        (await Client.SendAsync(edit)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Nor is it a right to a file the post keeps from moderation: the source names the file marked
    /// "Выбранные" outside every block, which the page cuts out for the moderator.
    /// </summary>
    [Fact]
    public async Task KeepAMarkedFileOfAPostFromAModerator()
    {
        var uploadId = Guid.NewGuid();
        var withFile = $"Карта: [img]https://dm.am/v1/uploads/{uploadId:D}/content[/img] ход";
        var postId = await SeedPost(withFile);
        await SeedMarkedAttachment(uploadId, postId);

        try
        {
            await ExpectTheEditKeptFromAModerator(postId, withFile, uploadId.ToString("D"));
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Set<Upload>().Where(u => u.UploadId == uploadId).ExecuteDeleteAsync();
            await DeletePost(postId);
        }
    }

    /// <summary>
    /// What the author sends is what the editor gets back: the spaces around the text and the case of
    /// the tag names are his (BBCODE_RENDERING.md, "Регистр имен").
    /// </summary>
    [Theory]
    [InlineData("  текст\n")]
    [InlineData("[B]x[/B]")]
    public async Task GiveTheAuthorBackTheTextAsHeSentIt(string typed)
    {
        var postId = await SeedPost("было");
        var author = new GeneralUser
        {
            UserId = TestConstants.SecondUserId,
            Username = TestConstants.SecondUserLogin,
            Role = UserRole.RegularUser
        };

        try
        {
            var edit = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/posts/{postId:D}", author);
            edit.Content = JsonContent.Create(new { gameText = typed, metagameText = typed });
            var saved = await Client.SendAsync(edit);
            saved.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", await saved.Content.ReadAsStringAsync());

            (await Text($"/v1/posts/{postId:D}", "gameText", author)).Should().Be(typed);
            (await Text($"/v1/posts/{postId:D}", "metagameText", author)).Should().Be(typed);
        }
        finally
        {
            await DeletePost(postId);
        }
    }

    /// <summary>A file of the post marked for a character of nobody in the test room.</summary>
    private async Task SeedMarkedAttachment(Guid uploadId, Guid postId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<Upload>().Add(new Upload
        {
            UploadId = uploadId,
            UserId = TestConstants.SecondUserId,
            Type = UploadType.PostAttachment,
            Status = UploadStatus.Confirmed,
            TargetPostId = postId,
            Original = true,
            ContentType = "image/png",
            SizeBytes = 10,
            ObjectKey = $"posts/{uploadId:N}.png",
            FileName = "karta.png",
            AddresseesJson = $"[{{\"character\":\"{Guid.NewGuid()}\",\"name\":\"Кто-то\",\"owner\":\"{Guid.NewGuid()}\"}}]",
            CreatedUtc = DateTimeOffset.UtcNow,
            ConfirmedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>The flag the page offers "Править" by.</summary>
    private async Task<bool> CanEdit(Guid postId, GeneralUser viewer)
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/posts/{postId:D}", viewer));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").GetProperty("viewer").GetProperty("canEdit").GetBoolean();
    }

    private async Task<string> Text(string url, string field, GeneralUser viewer)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, url, viewer);
        request.Headers.Add(BbAudienceHeader.HeaderName, "author_edit");
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resource").GetProperty(field).GetString()!;
    }

    private async Task<Guid> SeedTopic()
    {
        var topicId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var number = await db.Set<Topic>().Where(t => t.BoardId == TestConstants.TestBoardId)
            .MaxAsync(t => (int?)t.TopicNumber) ?? 0;
        db.Set<Topic>().Add(new Topic
        {
            TopicId = topicId,
            BoardId = TestConstants.TestBoardId,
            AuthorId = TestConstants.SecondUserId,
            Title = "Чужой топик",
            Text = Source,
            TopicNumber = number + 1,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return topicId;
    }

    /// <summary>A post of the second user in the test room, without a character.</summary>
    private async Task<Guid> SeedPost(string gameText)
    {
        var postId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<Post>().Add(new Post
        {
            PostId = postId,
            RoomId = TestConstants.TestRoomId,
            AuthorId = TestConstants.SecondUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            GameText = gameText,
            PrivateAddresseeSnapshotJson = "{}",
        });
        await db.SaveChangesAsync();
        return postId;
    }

    private async Task DeletePost(Guid postId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Set<Post>().Where(p => p.PostId == postId).ExecuteDeleteAsync();
    }
}
