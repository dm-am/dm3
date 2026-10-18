using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A like on a comment is on the comment the reader is handed back, in all four
/// discussions.
/// </summary>
/// <remarks>
/// The comment projection filled no likes, and nothing filled them after it: every
/// read answered with an empty list however many likes the row had. Nobody saw the
/// count, and the one who had liked saw an empty heart after a reload, so the click
/// meant to take the like back asked for a second one and was refused.
///
/// Written against HTTP rather than the services, as the message half of the same
/// proof is (<see cref="Messaging.PrivateMessageLikesShould" />): the omission lived
/// in a query, which every test with a substituted repository steps straight over.
/// The four discussions share that query, so each is asked through its own routes.
///
/// The comment is seeded with a marker in its text and listed by it: the forum topic
/// and the game are shared with the tests around this one, and a page of somebody
/// else's comments would not have this one on it.
/// </remarks>
public class CommentLikesShould : IntegrationTestBase
{
    public CommentLikesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Author => CustomWebApplicationFactory.CreateTestUser();
    private static GeneralUser Liker => CustomWebApplicationFactory.CreateSecondUser();

    public static TheoryData<string> Surfaces => new() { "forum", "game", "blog", "publication" };

    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task CarryTheLikeOnTheListedComment(string surface)
    {
        var seeded = await SeedAsync(surface);
        try
        {
            await LikeAsync(seeded);

            var listing = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, seeded.List, Author));

            listing.StatusCode.Should().Be(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
            var comment = document.RootElement
                .GetProperty("resources")
                .EnumerateArray()
                .Single(c => c.GetProperty("id").GetGuid() == seeded.CommentId);

            Names(comment).Should().Equal([Liker.Username],
                "the author of the comment is shown who liked it");
        }
        finally
        {
            await DropAsync(seeded);
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task CarryTheLikeOnTheSingleComment(string surface)
    {
        var seeded = await SeedAsync(surface);
        try
        {
            await LikeAsync(seeded);

            Names(await ReadAsync(seeded.Single, Liker)).Should().Equal([Liker.Username],
                "after a reload the liker has to see his own like to be able to take it back");
        }
        finally
        {
            await DropAsync(seeded);
        }
    }

    /// <summary>
    /// The client replaces the comment it holds with the answer to an edit, so the
    /// likes have to be on that answer too.
    /// </summary>
    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task CarryTheLikeOnTheAnswerToAnEdit(string surface)
    {
        var seeded = await SeedAsync(surface);
        try
        {
            await LikeAsync(seeded);

            var request = CreateAuthenticatedRequest(HttpMethod.Patch, seeded.Single, Author);
            request.Content = JsonContent.Create(new { text = $"Edited {seeded.Marker}" });
            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Names(document.RootElement.GetProperty("resource")).Should().Equal([Liker.Username]);
        }
        finally
        {
            await DropAsync(seeded);
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task RefuseARepeatedLikeAsAConflict(string surface)
    {
        var seeded = await SeedAsync(surface);
        try
        {
            await LikeAsync(seeded);

            var again = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Post, seeded.Likes, Liker));

            again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            await DropAsync(seeded);
        }
    }

    /// <summary>
    /// A like can only be taken back by the one who put it: anybody else has no like
    /// of his own on the comment, which is the state conflict the like path answers
    /// for, and the like stays.
    /// </summary>
    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task RefuseToTakeBackALikeTheReaderNeverPut(string surface)
    {
        var seeded = await SeedAsync(surface);
        try
        {
            await LikeAsync(seeded);

            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Delete, seeded.Likes, seeded.Stranger));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            Names(await ReadAsync(seeded.Single, Author)).Should().Equal([Liker.Username]);
        }
        finally
        {
            await DropAsync(seeded);
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task AnswerNotFoundOnTakingALikeOffAMissingComment(string surface)
    {
        var seeded = await SeedAsync(surface);
        try
        {
            var response = await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Delete, $"{seeded.Prefix}/{Guid.NewGuid()}/likes", Liker));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await DropAsync(seeded);
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task LetTheLikerTakeTheLikeBack(string surface)
    {
        var seeded = await SeedAsync(surface);
        try
        {
            await LikeAsync(seeded);

            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Delete, seeded.Likes, Liker));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            Names(await ReadAsync(seeded.Single, Liker)).Should().BeEmpty();
        }
        finally
        {
            await DropAsync(seeded);
        }
    }

    private sealed record Seeded(
        Guid CommentId, string Marker, string List, string Prefix, GeneralUser Stranger)
    {
        public string Single => $"{Prefix}/{CommentId}";
        public string Likes => $"{Single}/likes";
    }

    /// <summary>
    /// A comment by the test user on the given discussion, and a reader who has put
    /// no like on it.
    /// </summary>
    /// <remarks>
    /// The forum topic and the game are the seeded ones. The blog and the publication
    /// are made here: a publication is not seeded, and a blog made alongside it keeps
    /// the publication number out of the way of the seeded blogs.
    /// </remarks>
    private async Task<Seeded> SeedAsync(string surface)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var (entityId, list, prefix) = surface switch
        {
            "forum" => (TestConstants.TestTopicId, "/v1/topics/{0}/comments", "/v1/forum/comments"),
            "game" => (TestConstants.TestGameId, "/v1/games/{0}/comments", "/v1/games/comments"),
            "blog" => (AddBlog(dbContext).BlogId, "/v1/blogs/{0}/comments", "/v1/blogs/comments"),
            "publication" => (AddPublication(dbContext, AddBlog(dbContext).BlogId).PublicationId,
                "/v1/publications/{0}/comments", "/v1/publications/comments"),
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null)
        };

        var marker = Guid.NewGuid().ToString("N");
        var commentId = Guid.NewGuid();
        dbContext.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = entityId,
            AuthorId = Author.UserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = $"Comment {marker}"
        });

        var stranger = AddUser(dbContext);
        await dbContext.SaveChangesAsync();

        return new Seeded(
            commentId,
            marker,
            $"{string.Format(list, entityId)}?search={marker}&take=100",
            prefix,
            new GeneralUser { UserId = stranger.UserId, Username = stranger.Username, Role = UserRole.RegularUser });
    }

    /// <summary>
    /// The comment and everything written against it go, so the shared topic and
    /// game read as they did before.
    /// </summary>
    private async Task DropAsync(Seeded seeded)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Likes" WHERE "EntityId" = {0}""", seeded.CommentId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "CommentEdits" WHERE "CommentId" = {0}""", seeded.CommentId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Comments" WHERE "CommentId" = {0}""", seeded.CommentId);
    }

    private async Task LikeAsync(Seeded seeded)
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Post, seeded.Likes, Liker));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<JsonElement> ReadAsync(string address, GeneralUser reader)
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, address, reader));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").Clone();
    }

    private static string[] Names(JsonElement comment) => comment
        .GetProperty("likes")
        .EnumerateArray()
        .Select(l => l.GetProperty("username").GetString()!)
        .ToArray();

    private static DbUser AddUser(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"cls{userId:N}"[..20],
            Email = $"{userId:N}@commentlikes.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        return user;
    }

    private static DbBlog AddBlog(DmDbContext dbContext)
    {
        var blog = new DbBlog
        {
            BlogId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = Author.UserId,
            Title = "Comment likes blog",
            Description = "Seeded by CommentLikesShould",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        dbContext.Blogs.Add(blog);
        return blog;
    }

    private static DbPublication AddPublication(DmDbContext dbContext, Guid blogId)
    {
        var now = DateTimeOffset.UtcNow;
        var publication = new DbPublication
        {
            PublicationId = Guid.NewGuid(),
            BlogId = blogId,
            PublicationNumber = 1,
            AuthorId = Author.UserId,
            Title = "Comment likes entry",
            Content = "Seeded by CommentLikesShould",
            Preview = "Seeded",
            IsPublished = true,
            PublishedUtc = now,
            CreatedUtc = now
        };
        dbContext.Publications.Add(publication);
        return publication;
    }
}
