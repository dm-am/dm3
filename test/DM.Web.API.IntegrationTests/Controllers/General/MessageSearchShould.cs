using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// What a search preview may show is decided by the same rules as what the page
/// shows, and the preview is the harder half: it is built in the database, out of
/// a stored projection, for a reader the projection knows nothing about.
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
    /// The leak this pins had three links and every one of them was invisible.
    /// The [img] content pattern ran to the next [/img] anywhere in the post, so
    /// the unclosed tag joined up with the closing tag of the following image and
    /// swallowed the private block whole; what is swallowed is filed as a URL
    /// before the parse, so it never becomes a node and the visibility filter is
    /// never asked about it; and the plain-text walk that the stored search text
    /// is projected with handed it back word for word. ts_headline then opened a
    /// window around the PUBLIC word — the index had refused to tokenise the
    /// private one, which is what made it look safe — and the window ran straight
    /// through the block into the reply of every reader of the room.
    ///
    /// The reader here is neither the author, nor a game lead, nor an addressee,
    /// and the room is open, which is the ordinary case rather than an unusual
    /// one: the post is in TestGame, whose master is the test user.
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
    /// And the column itself holds none of it, which is the half that matters
    /// after the fact: a preview is one reader of this string and the next one
    /// written over it will be another.
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
    /// The two tests above pin the projection, and a projection is a process:
    /// every row written before the fix landed still carries what it wrote, and
    /// nothing rewrites them on read. So the preview has to cut the block out of
    /// the document it is handed, the way the fallback beside it always has —
    /// ts_headline reads that document rather than the vector, so the strip in
    /// the generated column decides which rows match and says nothing at all
    /// about what is shown.
    ///
    /// The column is poisoned here by hand, because that is the only way to hold
    /// this layer down on its own: with the parse fixed, no body a test can write
    /// produces such a row any more.
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
        /// Runs of the preview rather than a marked-up string, for the reason
        /// SnippetSegment carries: the marks are the database's and nobody escapes
        /// them.
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
