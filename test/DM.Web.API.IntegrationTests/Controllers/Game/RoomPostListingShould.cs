using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Xunit;
using static DM.Web.API.IntegrationTests.Helpers.RoomPostsTestHelper;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A room's posts read with the pulse's search, order and filters, each post still
/// carrying its number in the room.
/// </summary>
/// <remarks>
/// Over HTTP, because the parameters are the contract: a name the binder does not
/// know is ignored rather than refused, and the list then answers 200 with the
/// whole room, which reads as "the filter found everything".
///
/// One room of five live posts and a removed one between them, written a minute
/// apart:
///
///   #1 master, master's character  "Рассвет над рекой"                 +1          (review at +100 min)
///   #2 player, player's character  "Туман в долине"                    +1 +1 +1    (reviews at +10..+12)
///   #3 master, no character        "Мастер говорит: туман сгущается"   no reviews
///   -- master, master's character  removed
///   #4 player, player's character  "Ночь"                              -1          (review at +200)
///   #5 master, master's character  "Туман рассеялся"                   +1 +1       (reviews at +50, +51)
/// </remarks>
public class RoomPostListingShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public RoomPostListingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private sealed record Room(SeededRoom Seeded, Guid[] Posts)
    {
        public Guid RoomId => Seeded.RoomId;
    }

    /// <summary>
    /// A post keeps its place in the room on every view of it. The removed post
    /// between #3 and #4 is what tells the room's numbering from a count of rows
    /// that forgot the soft delete.
    /// </summary>
    [Fact]
    public async Task NumberEveryPostByItsPlaceInTheRoomWhateverTheView()
    {
        var room = await SeedAsync();

        var asWritten = await ReadAsync(room, "");
        asWritten.Ids.Should().Equal(room.Posts);
        asWritten.Numbers.Should().Equal(1, 2, 3, 4, 5);
        asWritten.Total.Should().Be(5, "the removed post is not in the room");

        // A stretch of the room: the numbers are the room's, not the page's.
        var secondPage = await ReadAsync(room, "skip=2&take=2");
        secondPage.Ids.Should().Equal(room.Posts[2], room.Posts[3]);
        secondPage.Numbers.Should().Equal(3, 4);

        var newestFirst = await ReadAsync(room, "sortOrder=desc&take=2");
        newestFirst.Ids.Should().Equal(room.Posts[4], room.Posts[3]);
        newestFirst.Numbers.Should().Equal(5, 4);

        var byRating = await ReadAsync(room, "sortBy=rating&sortOrder=desc");
        byRating.Ids.Should().Equal(room.Posts[1], room.Posts[4], room.Posts[0], room.Posts[2], room.Posts[3]);
        byRating.Numbers.Should().Equal(2, 5, 1, 3, 4);

        var found = await ReadAsync(room, "search=туман");
        found.Ids.Should().Equal(room.Posts[1], room.Posts[2], room.Posts[4]);
        found.Numbers.Should().Equal(2, 3, 5);
        found.Total.Should().Be(3);
    }

    [Fact]
    public async Task FilterTheRoomByAuthorsCharactersRatingReviewsAndDates()
    {
        var room = await SeedAsync();
        var seeded = room.Seeded;

        (await ReadAsync(room, $"authorUsernames={seeded.Player.Username}")).Ids
            .Should().Equal(room.Posts[1], room.Posts[3]);

        (await ReadAsync(room, $"characterIds={seeded.MasterCharacterId}")).Ids
            .Should().Equal(room.Posts[0], room.Posts[4]);

        (await ReadAsync(room, "withoutCharacter=true")).Ids
            .Should().Equal(new[] { room.Posts[2] }, "alone, the flag keeps the posts without a character");

        (await ReadAsync(room, $"characterIds={seeded.PlayerCharacterId}&withoutCharacter=true")).Ids
            .Should().Equal(new[] { room.Posts[1], room.Posts[2], room.Posts[3] },
                "with characters, the flag adds the posts without one to theirs");

        (await ReadAsync(room,
                $"characterIds={seeded.PlayerCharacterId}&characterIds={seeded.MasterCharacterId}")).Ids
            .Should().Equal(room.Posts[0], room.Posts[1], room.Posts[3], room.Posts[4]);

        (await ReadAsync(room, "minRating=1&maxRating=2")).Ids
            .Should().Equal(room.Posts[0], room.Posts[4]);

        (await ReadAsync(room, "maxRating=-1")).Ids
            .Should().Equal(new[] { room.Posts[3] }, "a rating can be negative");

        (await ReadAsync(room, "hasReviews=false")).Ids
            .Should().Equal(new[] { room.Posts[2] }, "false asks for the posts nobody reviewed");

        (await ReadAsync(room, "hasReviews=true")).Ids
            .Should().Equal(room.Posts[0], room.Posts[1], room.Posts[3], room.Posts[4]);

        var from = Uri.EscapeDataString(Start.AddMinutes(1).ToString("O"));
        var to = Uri.EscapeDataString(Start.AddMinutes(2).ToString("O"));
        var byDate = await ReadAsync(room, $"createdFromUtc={from}&createdToUtc={to}");
        byDate.Ids.Should().Equal(room.Posts[1], room.Posts[2]);
        byDate.Numbers.Should().Equal(2, 3);

        var narrowed = await ReadAsync(room,
            $"authorUsernames={seeded.Master.Username}&search=туман&hasReviews=true");
        narrowed.Ids.Should().Equal(new[] { room.Posts[4] }, "the filters narrow together");
        narrowed.Numbers.Should().Equal(5);
        narrowed.Total.Should().Be(1);
    }

    /// <summary>
    /// The review orders, ties closed on the writing order in the same direction.
    /// </summary>
    [Fact]
    public async Task SortTheRoomByReviewCountAndByTheLatestReview()
    {
        var room = await SeedAsync();

        // #1 and #4 have one review each: newest first among them, like the rest.
        (await ReadAsync(room, "sortBy=reviewcount&sortOrder=desc")).Ids
            .Should().Equal(room.Posts[1], room.Posts[4], room.Posts[3], room.Posts[0], room.Posts[2]);

        (await ReadAsync(room, "sortBy=reviewcount")).Ids
            .Should().Equal(room.Posts[2], room.Posts[0], room.Posts[3], room.Posts[4], room.Posts[1]);

        // #3 has no review, and goes last both ways: a descending order would open on
        // it otherwise, since PostgreSQL puts NULL first there.
        (await ReadAsync(room, "sortBy=lastreview&sortOrder=desc")).Ids
            .Should().Equal(room.Posts[3], room.Posts[0], room.Posts[4], room.Posts[1], room.Posts[2]);

        (await ReadAsync(room, "sortBy=lastreview&sortOrder=asc")).Ids
            .Should().Equal(room.Posts[1], room.Posts[4], room.Posts[0], room.Posts[3], room.Posts[2]);
    }

    [Fact]
    public async Task RefuseASortTheRoomDoesNotHave()
    {
        var room = await SeedAsync();

        var response = await Client.GetAsync($"/v1/rooms/{room.RoomId}/posts?sortBy=likes");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Filters do not open a room: a private room stays absent to whoever it is not
    /// open to, filtered or not.
    /// </summary>
    [Fact]
    public async Task KeepAPrivateRoomClosedToAFilteredRead()
    {
        var seeded = await AddGameWithRoomAsync(DatabaseFixture, RoomAccessType.Private);
        await AddPostAsync(DatabaseFixture, seeded.RoomId, seeded.Master.UserId, null, Start, "Туман");

        var response = await Client.GetAsync(
            $"/v1/rooms/{seeded.RoomId}/posts?search=туман&sortBy=rating&sortOrder=desc");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The number travels on the single-post reads and on the answer to an edit,
    /// which is what the client puts back into the page it shows.
    /// </summary>
    [Fact]
    public async Task NumberTheSinglePostAndTheEditedPost()
    {
        var room = await SeedAsync();

        var single = await Client.GetAsync($"/v1/posts/{room.Posts[3]}");
        single.StatusCode.Should().Be(HttpStatusCode.OK);
        NumberOf(await single.Content.ReadAsStringAsync()).Should().Be(4);

        var request = CreateAuthenticatedRequest(
            HttpMethod.Patch, $"/v1/posts/{room.Posts[4]}", room.Seeded.Master);
        request.Content = JsonContent.Create(new { gameText = "Туман рассеялся совсем" });
        var edited = await Client.SendAsync(request);
        var body = await edited.Content.ReadAsStringAsync();
        edited.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        NumberOf(body).Should().Be(5);
    }

    private async Task<Room> SeedAsync()
    {
        var seeded = await AddGameWithRoomAsync(DatabaseFixture);
        var master = seeded.Master.UserId;
        var player = seeded.Player.UserId;

        var first = await AddPostAsync(DatabaseFixture, seeded.RoomId, master,
            seeded.MasterCharacterId, Start, "Рассвет над рекой");
        var second = await AddPostAsync(DatabaseFixture, seeded.RoomId, player,
            seeded.PlayerCharacterId, Start.AddMinutes(1), "Туман в долине");
        var third = await AddPostAsync(DatabaseFixture, seeded.RoomId, master,
            null, Start.AddMinutes(2), "Мастер говорит: туман сгущается");
        await AddPostAsync(DatabaseFixture, seeded.RoomId, master,
            seeded.MasterCharacterId, Start.AddMinutes(3), "Туман, который удалили", isRemoved: true);
        var fourth = await AddPostAsync(DatabaseFixture, seeded.RoomId, player,
            seeded.PlayerCharacterId, Start.AddMinutes(4), "Ночь");
        var fifth = await AddPostAsync(DatabaseFixture, seeded.RoomId, master,
            seeded.MasterCharacterId, Start.AddMinutes(5), "Туман рассеялся");

        await AddReviewsAsync(DatabaseFixture, seeded, first, master, (1, Start.AddMinutes(100)));
        await AddReviewsAsync(DatabaseFixture, seeded, second, player,
            (1, Start.AddMinutes(10)), (1, Start.AddMinutes(11)), (1, Start.AddMinutes(12)));
        await AddReviewsAsync(DatabaseFixture, seeded, fourth, player, (-1, Start.AddMinutes(200)));
        await AddReviewsAsync(DatabaseFixture, seeded, fifth, master,
            (1, Start.AddMinutes(50)), (1, Start.AddMinutes(51)));

        return new Room(seeded, [first, second, third, fourth, fifth]);
    }

    private sealed record Page(Guid[] Ids, int?[] Numbers, int Total);

    private async Task<Page> ReadAsync(Room room, string query)
    {
        var response = await Client.GetAsync($"/v1/rooms/{room.RoomId}/posts?{query}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "GET ?{0} answered {1}", query, body);

        using var document = JsonDocument.Parse(body);
        var resources = document.RootElement.GetProperty("resources").EnumerateArray().ToArray();

        return new Page(
            resources.Select(r => r.GetProperty("id").GetGuid()).ToArray(),
            resources.Select(r => r.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number
                ? n.GetInt32()
                : (int?)null).ToArray(),
            document.RootElement.GetProperty("paging").GetProperty("total").GetInt32());
    }

    private static int? NumberOf(string envelope)
    {
        using var document = JsonDocument.Parse(envelope);
        var number = document.RootElement.GetProperty("resource").GetProperty("number");
        return number.ValueKind == JsonValueKind.Number ? number.GetInt32() : null;
    }
}
