using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;
using static DM.Web.API.IntegrationTests.Helpers.RoomPostsTestHelper;
using DbPostEdit = DM.Infrastructure.Persistence.Entities.Game.Posts.PostEdit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// The rated feed carries a post's edits, so its card can say the post was edited
/// and by whom, as the room does.
/// </summary>
/// <remarks>
/// The feed used to clear the list after the shared projection had read it: the
/// card showed no edit line, so a post rewritten after its reviews read in the
/// feed as if it were the text that had been reviewed.
/// </remarks>
public class RatedPostEditsShould : IntegrationTestBase
{
    public RatedPostEditsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CarryTheEditsOfAPostOnTheRatedFeed()
    {
        var seeded = await AddGameWithRoomAsync(DatabaseFixture);
        var postId = await AddPostAsync(DatabaseFixture, seeded.RoomId, seeded.Master.UserId,
            seeded.MasterCharacterId, DateTimeOffset.UtcNow.AddHours(-2));
        var editedUtc = DateTimeOffset.UtcNow.AddHours(-1);

        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Set<DbPostEdit>().Add(new DbPostEdit
            {
                PostEditId = Guid.NewGuid(),
                PostId = postId,
                EditorUserId = seeded.Player.UserId,
                ModifiedUtc = editedUtc,
            });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync($"/v1/posts?gameId={seeded.GameId}&sortBy=created");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        using var document = JsonDocument.Parse(body);
        var post = document.RootElement.GetProperty("resources").EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == postId);
        var edits = post.GetProperty("edits").EnumerateArray().ToArray();

        edits.Should().ContainSingle("the post was edited once");
        edits[0].GetProperty("editor").GetProperty("username").GetString()
            .Should().Be(seeded.Player.Username);
        edits[0].GetProperty("modifiedUtc").GetDateTimeOffset()
            .Should().BeCloseTo(editedUtc, TimeSpan.FromSeconds(1));
    }
}
