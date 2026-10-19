using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A search preview follows the rules of the page, though it is built in the database for a reader the
/// projection knows nothing about.
/// </summary>
public class MessageSearchShould : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The word standing in the public half of the post.</summary>
    private const string PublicWord = "якорьметка";

    /// <summary>The word standing inside the [private] block.</summary>
    private const string PrivateWord = "секретпароль";

    public MessageSearchShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// A private block inside an image tag the author never closed stays out of
    /// the results of a stranger's search.
    /// </summary>
    /// <remarks>
    /// ts_headline opens a window around a public word, so a block swallowed into an unclosed image would run
    /// into the preview. The reader is a stranger in an open room of TestGame.
    /// </remarks>
    [Fact]
    public async Task ShowNoPrivateTextToAStrangerSearchingForAWordBesideIt()
    {
        var postId = await CreatePost(
            $"Разведка вернулась к {PublicWord} и доложила обстановку.\n" +
            "[img]https://example.com/karta.png\n" +
            $"[private=\"Гончая\"]{PrivateWord} в отряде есть предатель[/private]\n" +
            "[img]https://example.com/vtoraya.png[/img]\n" +
            "Конец поста.");
        try
        {
            var hits = await Search(PublicWord);

            // Found, because the public half of the post is indexed: a test that
            // passes because the row disappeared proves nothing about the preview.
            var hit = hits.Should().ContainSingle(h => h.Id == postId).Subject;

            var preview = string.Concat(hit.Snippet.Select(segment => segment.Text));
            preview.Should().Contain(PublicWord,
                "the window is built around the word that was searched for");
            preview.Should().NotContain(PrivateWord,
                "the block is private on the page, and a preview is a page too");
            preview.Should().NotContain("private",
                "neither the block nor the tag that carries it belongs in a preview");
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    /// <summary>
    /// The column itself holds none of it.
    /// </summary>
    [Fact]
    public async Task KeepPrivateTextOutOfTheStoredProjectionEntirely()
    {
        var postId = await CreatePost(
            $"Разведка вернулась к {PublicWord}.\n" +
            "[img]https://example.com/karta.png\n" +
            $"[private=\"Гончая\"]{PrivateWord}[/private]\n" +
            "[img]https://example.com/vtoraya.png[/img]");
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var projected = await dbContext.Posts
                .Where(p => p.PostId == postId)
                .Select(p => EF.Property<string>(p, "SearchText"))
                .SingleAsync();

            projected.Should().Contain(PublicWord);
            projected.Should().NotContain(PrivateWord);
            projected.Should().NotContain("private");
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    /// <summary>
    /// A row whose projection was written before the projection learned to drop
    /// the block still previews without it.
    /// </summary>
    /// <remarks>
    /// ts_headline reads the document, not the vector, so the preview cuts the block out itself. The column is
    /// poisoned by hand: no body a test can write produces such a row.
    /// </remarks>
    [Fact]
    public async Task ShowNoPrivateTextFromAProjectionWrittenBeforeTheFix()
    {
        var postId = await CreatePost($"Разведка вернулась к {PublicWord}.");
        try
        {
            await PoisonProjection(postId,
                $"Разведка вернулась к {PublicWord} и доложила обстановку. " +
                $"[private=\"Гончая\"]{PrivateWord} в отряде есть предатель[/private] Конец поста.");

            var hits = await Search(PublicWord);

            var hit = hits.Should().ContainSingle(h => h.Id == postId).Subject;
            var preview = string.Concat(hit.Snippet.Select(segment => segment.Text));

            preview.Should().Contain(PublicWord);
            preview.Should().NotContain(PrivateWord);
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    private async Task<MessageSearchRow[]> Search(string query)
    {
        var url = $"/v1/search/messages?search={Uri.EscapeDataString(query)}";
        // A stranger to the post: not its author, not a lead of its game, not an
        // addressee of the block. The room is open, so they may read it.
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, url, CustomWebApplicationFactory.CreateSecondUser()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MessageSearchEnvelope>(JsonOptions);
        return payload!.Resources.ToArray();
    }

    private async Task<Guid> CreatePost(string gameText)
    {
        var postId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Posts.Add(new Post
        {
            PostId = postId,
            RoomId = TestConstants.TestRoomId,
            AuthorId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            GameText = gameText,
            PrivateAddresseeSnapshotJson = "{}",
            IsRemoved = false,
        });
        await dbContext.SaveChangesAsync();
        return postId;
    }

    /// <summary>
    /// Write the projection a row would have carried before the parse was fixed.
    /// </summary>
    private async Task PoisonProjection(Guid postId, string searchText)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """UPDATE "Posts" SET "SearchText" = {1} WHERE "PostId" = {0}""", postId, searchText);
    }


    private class MessageSearchEnvelope
    {
        public IEnumerable<MessageSearchRow> Resources { get; set; } = [];
    }

    private class MessageSearchRow
    {
        public string SourceType { get; set; } = "";
        public Guid Id { get; set; }

        /// <summary>
        /// Runs of the preview rather than a marked-up string, as SnippetSegment says.
        /// </summary>
        public IReadOnlyList<SnippetSegment> Snippet { get; set; } = [];
    }

    /// <summary>One run of a preview, as the wire carries it.</summary>
    private class SnippetSegment
    {
        public string Text { get; set; } = "";
        public bool IsMatch { get; set; }
    }
}
