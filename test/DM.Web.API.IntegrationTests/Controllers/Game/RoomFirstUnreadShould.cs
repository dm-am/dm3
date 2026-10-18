using System.Net;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Xunit;
using static DM.Web.API.IntegrationTests.Helpers.RoomPostsTestHelper;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Where a room opens when no page is named: on the first unread post, or on the
/// last page when everything is read.
/// </summary>
/// <remarks>
/// Asked over HTTP and checked against the room's own list: the number and the
/// page are only right if the list, read at that page, shows that post at that
/// number.
/// </remarks>
public class RoomFirstUnreadShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public RoomFirstUnreadShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Five live posts a minute apart and a removed one among them, read up to the
    /// third: the fourth is the first unread. The removed post is written after the
    /// read mark, so a count that forgot the soft delete would call it unread.
    /// </summary>
    [Fact]
    public async Task OpenTheRoomOnTheFirstUnreadPost()
    {
        var (seeded, posts) = await SeedAsync();
        await MarkReadAsync(DatabaseFixture, seeded, seeded.Player.UserId,
            Start.AddMinutes(2).AddSeconds(30).UtcDateTime);

        // A filtered read of the room first: it marks nothing, so the answer below
        // is the same one the reader would have had without it.
        (await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get,
            $"/v1/rooms/{seeded.RoomId}/posts?search=нет&sortBy=rating", seeded.Player)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await FirstUnreadAsync(seeded, seeded.Player, "take=3");

        result.PostId.Should().Be(posts[3]);
        result.PostNumber.Should().Be(4);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(3);
        result.TotalUnreadCount.Should().Be(2, "the removed post is not unread, it is not there");
        result.HasUnread.Should().BeTrue();

        // The page named is the page that shows the post, at that number.
        var page = await PageAsync(seeded, skip: (result.Page - 1) * result.PageSize, take: result.PageSize);
        page.Should().Contain((posts[3], 4));

        // Without a size, the reader's own posts-per-page setting.
        var byDefault = await FirstUnreadAsync(seeded, seeded.Player, "");
        byDefault.PageSize.Should().Be(20);
        byDefault.Page.Should().Be(1);
    }

    /// <summary>
    /// The room is marked read when it is opened, not when a post is sent from it,
    /// so the reader's own post stands after their mark. It is not news to them,
    /// and neither is what they answered: the room opens on the post after theirs.
    /// </summary>
    [Fact]
    public async Task NotOpenTheRoomOnTheReadersOwnPost()
    {
        var seeded = await AddGameWithRoomAsync(DatabaseFixture);
        var master = seeded.Master.UserId;
        var player = seeded.Player.UserId;

        await AddPostAsync(DatabaseFixture, seeded.RoomId, master, null, Start);
        await MarkReadAsync(DatabaseFixture, seeded, player, Start.AddSeconds(30).UtcDateTime);
        await AddPostAsync(DatabaseFixture, seeded.RoomId, master, null, Start.AddMinutes(1));
        var own = await AddPostAsync(DatabaseFixture, seeded.RoomId, player, seeded.PlayerCharacterId,
            Start.AddMinutes(2));

        var readToOwn = await FirstUnreadAsync(seeded, seeded.Player, "take=20");
        readToOwn.HasUnread.Should().BeFalse("nothing was written after the reader's own post");
        readToOwn.PostId.Should().Be(own);
        readToOwn.PostNumber.Should().Be(3);

        var answer = await AddPostAsync(DatabaseFixture, seeded.RoomId, master, null, Start.AddMinutes(3));

        var result = await FirstUnreadAsync(seeded, seeded.Player, "take=2");
        result.HasUnread.Should().BeTrue();
        result.PostId.Should().Be(answer);
        result.PostNumber.Should().Be(4);
        result.Page.Should().Be(2);
        result.TotalUnreadCount.Should().Be(1);

        // Each reader is lifted to their own post only. The master never marked the
        // room, and wrote its last post: for him it is read to the end.
        var forMaster = await FirstUnreadAsync(seeded, seeded.Master, "take=20");
        forMaster.HasUnread.Should().BeFalse();
        forMaster.PostId.Should().Be(answer);
    }

    [Fact]
    public async Task OpenARoomReadToTheEndOnItsLastPage()
    {
        var (seeded, posts) = await SeedAsync();
        await MarkReadAsync(DatabaseFixture, seeded, seeded.Player.UserId, Start.AddHours(1).UtcDateTime);

        var result = await FirstUnreadAsync(seeded, seeded.Player, "take=2");

        result.HasUnread.Should().BeFalse();
        result.PostId.Should().Be(posts[4], "the reader is put down at the last post");
        result.PostNumber.Should().Be(5);
        result.Page.Should().Be(3, "five posts two to a page end on the third");
        result.TotalUnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task OpenARoomNeverReadOnItsFirstPost()
    {
        var (seeded, posts) = await SeedAsync();

        var signedIn = await FirstUnreadAsync(seeded, seeded.Player, "take=2");
        signedIn.PostId.Should().Be(posts[0]);
        signedIn.PostNumber.Should().Be(1);
        signedIn.Page.Should().Be(1);
        signedIn.TotalUnreadCount.Should().Be(5);
        signedIn.HasUnread.Should().BeTrue();

        // Nothing is marked for a reader who is not signed in: the first post too.
        var guest = await FirstUnreadAsync(seeded, null, "take=2");
        guest.PostId.Should().Be(posts[0]);
        guest.PostNumber.Should().Be(1);
        guest.HasUnread.Should().BeTrue();
    }

    /// <summary>
    /// Posts written in one instant are read or unread together, and the first of
    /// them is the one the page shows first, which is decided by the identifier.
    /// </summary>
    [Fact]
    public async Task NameThePostThePageShowsFirstAmongPostsOfOneInstant()
    {
        var seeded = await AddGameWithRoomAsync(DatabaseFixture);
        await AddPostAsync(DatabaseFixture, seeded.RoomId, seeded.Master.UserId, null, Start);
        for (var i = 0; i < 4; i++)
        {
            await AddPostAsync(DatabaseFixture, seeded.RoomId, seeded.Master.UserId, null, Start.AddMinutes(1));
        }

        await MarkReadAsync(DatabaseFixture, seeded, seeded.Player.UserId, Start.AddSeconds(30).UtcDateTime);

        var result = await FirstUnreadAsync(seeded, seeded.Player, "take=20");
        var page = await PageAsync(seeded, skip: 0, take: 20);

        result.PostNumber.Should().Be(2);
        result.TotalUnreadCount.Should().Be(4);
        page[1].Should().Be((result.PostId!.Value, 2), "the page shows it second, at number 2");
    }

    [Fact]
    public async Task AnswerAnEmptyRoomWithItsFirstPage()
    {
        var seeded = await AddGameWithRoomAsync(DatabaseFixture);

        var result = await FirstUnreadAsync(seeded, seeded.Player, "");

        result.PostId.Should().BeNull();
        result.PostNumber.Should().Be(0);
        result.Page.Should().Be(1);
        result.HasUnread.Should().BeFalse();
    }

    [Fact]
    public async Task KeepAPrivateRoomAbsentAndRefuseAPageSizeOutOfRange()
    {
        var hidden = await AddGameWithRoomAsync(DatabaseFixture, RoomAccessType.Private);
        (await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get,
                $"/v1/rooms/{hidden.RoomId}/posts/first-unread", hidden.Player)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "a room outside the reader's scope is absent");

        var open = await AddGameWithRoomAsync(DatabaseFixture);
        (await Client.GetAsync($"/v1/rooms/{open.RoomId}/posts/first-unread?take=0"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync($"/v1/rooms/{open.RoomId}/posts/first-unread?take=201"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(SeededRoom Seeded, Guid[] Posts)> SeedAsync()
    {
        var seeded = await AddGameWithRoomAsync(DatabaseFixture);
        var master = seeded.Master.UserId;

        var posts = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            posts.Add(await AddPostAsync(DatabaseFixture, seeded.RoomId, master, null, Start.AddMinutes(i)));
        }

        await AddPostAsync(DatabaseFixture, seeded.RoomId, master, null, Start.AddMinutes(3), isRemoved: true);

        for (var i = 4; i < 6; i++)
        {
            posts.Add(await AddPostAsync(DatabaseFixture, seeded.RoomId, master, null, Start.AddMinutes(i)));
        }

        return (seeded, posts.ToArray());
    }

    private sealed record FirstUnread(
        Guid? PostId, int PostNumber, int Page, int PageSize, int TotalUnreadCount, bool HasUnread);

    private async Task<FirstUnread> FirstUnreadAsync(SeededRoom seeded, DM.Domain.Core.Dto.GeneralUser? reader, string query)
    {
        var url = $"/v1/rooms/{seeded.RoomId}/posts/first-unread?{query}";
        var response = reader is null
            ? await Client.GetAsync(url)
            : await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, reader));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        using var document = JsonDocument.Parse(body);
        var resource = document.RootElement.GetProperty("resource");

        // A null is left out of the response altogether.
        return new FirstUnread(
            resource.TryGetProperty("postId", out var postId) && postId.ValueKind != JsonValueKind.Null
                ? postId.GetGuid()
                : null,
            resource.GetProperty("postNumber").GetInt32(),
            resource.GetProperty("page").GetInt32(),
            resource.GetProperty("pageSize").GetInt32(),
            resource.GetProperty("totalUnreadCount").GetInt32(),
            resource.GetProperty("hasUnread").GetBoolean());
    }

    private async Task<List<(Guid Id, int Number)>> PageAsync(SeededRoom seeded, int skip, int take)
    {
        var response = await Client.GetAsync($"/v1/rooms/{seeded.RoomId}/posts?skip={skip}&take={take}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resources").EnumerateArray()
            .Select(r => (r.GetProperty("id").GetGuid(), r.GetProperty("number").GetInt32()))
            .ToList();
    }
}
