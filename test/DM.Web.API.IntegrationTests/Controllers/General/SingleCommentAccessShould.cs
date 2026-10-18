using System.Net;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A comment read by its identifier answers as the discussion it hangs on does,
/// and only its own discussion's route hands it out.
/// </summary>
/// <remarks>
/// The four discussions keep their comments in one table, and the single read takes
/// the row by its id: a game comment asked for through the forum route, which
/// checks nothing of games, would come back with its text and the names of
/// everybody who liked it. The listing asks the discussion first, and the single
/// read is held to the listing's answer.
/// </remarks>
public class SingleCommentAccessShould : IntegrationTestBase
{
    public SingleCommentAccessShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The seeded game whose discussion only its people may write in, led by the
    /// admin.
    /// </summary>
    private static readonly Guid ReadOnlyGameId = TestConstants.NoRecruitmentGameId;

    /// <summary>
    /// A discussion only its people may write in is read by everybody, the guest
    /// included, by the listing and by the single read alike.
    /// </summary>
    [Fact]
    public async Task GiveACommentOfAReadOnlyDiscussionToEveryReaderAsTheListingDoes()
    {
        var (commentId, marker, stranger) = await SeedAsync(ReadOnlyGameId);
        try
        {
            var single = $"/v1/games/comments/{commentId}";
            var listing = $"/v1/games/{ReadOnlyGameId}/comments?take=100&sortOrder=desc";

            foreach (var reader in new GeneralUser?[] { null, stranger })
            {
                var listed = await Client.SendAsync(Request(listing, reader));
                listed.StatusCode.Should().Be(HttpStatusCode.OK, "the discussion is read by everybody");
                (await listed.Content.ReadAsStringAsync()).Should().Contain(marker);

                var read = await Client.SendAsync(Request(single, reader));
                read.StatusCode.Should().Be(HttpStatusCode.OK,
                    "the single read answers the reader the way the listing does");
                (await read.Content.ReadAsStringAsync()).Should().Contain(marker);
            }

            var master = await Client.SendAsync(Request(single, CustomWebApplicationFactory.CreateAdminUser()));
            master.StatusCode.Should().Be(HttpStatusCode.OK, "the game's master reads its discussion");
            (await master.Content.ReadAsStringAsync()).Should().Contain(marker);
        }
        finally
        {
            await DropAsync(commentId);
        }
    }

    public static TheoryData<string, string> ForeignRoutes => new()
    {
        { "game", "/v1/forum/comments" },
        { "game", "/v1/blogs/comments" },
        { "game", "/v1/publications/comments" },
        { "forum", "/v1/games/comments" },
    };

    /// <summary>
    /// A comment asked for through another discussion's route answers as a comment
    /// that is not there: that route knows nothing of the discussion it hangs on,
    /// so it has nothing to check it by.
    /// </summary>
    [Theory]
    [MemberData(nameof(ForeignRoutes))]
    public async Task AnswerNotFoundThroughAnotherDiscussionsRoute(string owner, string prefix)
    {
        var entityId = owner == "game" ? TestConstants.TestGameId : TestConstants.TestTopicId;
        var (commentId, marker, _) = await SeedAsync(entityId);
        try
        {
            var response = await Client.SendAsync(Request($"{prefix}/{commentId}", null));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await response.Content.ReadAsStringAsync()).Should().NotContain(marker);
        }
        finally
        {
            await DropAsync(commentId);
        }
    }

    private static HttpRequestMessage Request(string url, GeneralUser? reader) => reader is null
        ? new HttpRequestMessage(HttpMethod.Get, url)
        : CreateAuthenticatedRequest(HttpMethod.Get, url, reader);

    /// <summary>
    /// A comment by the admin on the given discussion, and a reader who has no part
    /// in any of them.
    /// </summary>
    private async Task<(Guid CommentId, string Marker, GeneralUser Stranger)> SeedAsync(Guid entityId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var marker = Guid.NewGuid().ToString("N");
        var commentId = Guid.NewGuid();
        dbContext.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = entityId,
            AuthorId = TestConstants.AdminUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = $"Comment {marker}"
        });

        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"sca{userId:N}"[..20],
            Email = $"{userId:N}@singlecommentaccess.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return (commentId, marker,
            new GeneralUser { UserId = userId, Username = user.Username, Role = UserRole.RegularUser });
    }

    private async Task DropAsync(Guid commentId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Comments" WHERE "CommentId" = {0}""", commentId);
    }
}
