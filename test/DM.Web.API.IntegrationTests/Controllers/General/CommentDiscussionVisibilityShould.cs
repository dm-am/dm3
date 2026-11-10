using System.Net;
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

// A comment of a hidden discussion, of another kind, and an id that is nothing must answer alike,
// body included, as the publication and blog visibility tests next door assert.
public class CommentDiscussionVisibilityShould : IntegrationTestBase
{
    public CommentDiscussionVisibilityShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static readonly string[] SingleReads =
    [
        "/v1/forum/comments",
        "/v1/games/comments",
        "/v1/blogs/comments",
        "/v1/publications/comments"
    ];

    public static TheoryData<bool> Callers => new() { true, false };

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task AnswerOnACommentOfAPrivateDraftBlogAsOnNoCommentAtAll(bool signedIn)
    {
        var seeded = await SeedAsync();
        var stranger = signedIn ? seeded.Stranger : null;

        var onMissing = await Read($"/v1/blogs/comments/{Guid.NewGuid()}", stranger);
        var inPrivateDraft = await Read($"/v1/blogs/comments/{seeded.InPrivateDraftBlog}", stranger);

        onMissing.Status.Should().Be(HttpStatusCode.NotFound);
        inPrivateDraft.Should().Be(onMissing, "a comment of a private draft is not the reader's to know about");
    }

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task AnswerOnTheDiscussionOfAPrivateDraftBlogAsOnNoBlogAtAll(bool signedIn)
    {
        var seeded = await SeedAsync();
        var stranger = signedIn ? seeded.Stranger : null;

        var onMissing = await Read($"/v1/blogs/{Guid.NewGuid()}/comments", stranger);
        var ofPrivateDraft = await Read($"/v1/blogs/{seeded.PrivateDraftBlog}/comments", stranger);

        onMissing.Status.Should().Be(HttpStatusCode.NotFound);
        ofPrivateDraft.Should().Be(onMissing,
            "the discussion of a blog nobody may open is listed to nobody, by GUID as by alias");
    }

    [Fact]
    public async Task AnswerOnALikeOfACommentOfAPrivateDraftBlogAsOnNoCommentAtAll()
    {
        var seeded = await SeedAsync();

        var onMissing = await Send(HttpMethod.Post, $"/v1/blogs/comments/{Guid.NewGuid()}/likes", seeded.Stranger);
        var inPrivateDraft = await Send(HttpMethod.Post,
            $"/v1/blogs/comments/{seeded.InPrivateDraftBlog}/likes", seeded.Stranger);

        onMissing.Status.Should().Be(HttpStatusCode.NotFound);
        inPrivateDraft.Should().Be(onMissing, "a like is a read of the comment first");
    }

    // The gate is about who may open the blog: its author still reads the comment, and a comment of
    // an open blog is read by anybody.
    [Fact]
    public async Task StillGiveTheCommentToItsBlogAndToEveryReaderOfAnOpenBlog()
    {
        var seeded = await SeedAsync();

        (await Read($"/v1/blogs/comments/{seeded.InPrivateDraftBlog}", seeded.Author)).Status
            .Should().Be(HttpStatusCode.OK);
        (await Read($"/v1/blogs/comments/{seeded.InOpenBlog}", seeded.Stranger)).Status
            .Should().Be(HttpStatusCode.OK);
        (await Read($"/v1/blogs/comments/{seeded.InOpenBlog}", null)).Status
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Every single read answers on a comment of another kind of discussion
    /// exactly as on an id that is nothing.
    /// </summary>
    [Fact]
    public async Task AnswerOnACommentOfAnotherDiscussionAsOnNoCommentAtAll()
    {
        var seeded = await SeedAsync(inSharedDiscussions: true);
        try
        {
            foreach (var route in SingleReads)
            {
                var onMissing = await Read($"{route}/{Guid.NewGuid()}", seeded.Stranger);
                onMissing.Status.Should().Be(HttpStatusCode.NotFound);

                foreach (var (owner, commentId) in seeded.OfEveryDiscussion)
                {
                    if (route.Contains(owner, StringComparison.Ordinal)) continue;

                    (await Read($"{route}/{commentId}", seeded.Stranger)).Should().Be(onMissing,
                        $"{route} is asked about a comment of a {owner} discussion");
                }
            }
        }
        finally
        {
            await DropSharedAsync(seeded);
        }
    }

    [Fact]
    public async Task AnswerOnACommentOfAPublicationInAHiddenBlogAsOnNoCommentAtAll()
    {
        var seeded = await SeedAsync();

        var onMissing = await Read($"/v1/publications/comments/{Guid.NewGuid()}", seeded.Stranger);
        var inHidden = await Read($"/v1/publications/comments/{seeded.InHiddenPublication}", seeded.Stranger);

        inHidden.Should().Be(onMissing);
    }

    // Status and body together: the body is what tells two 404s apart.
    private sealed record Answer(HttpStatusCode Status, string Body);

    private Task<Answer> Read(string url, GeneralUser? caller) => Send(HttpMethod.Get, url, caller);

    private async Task<Answer> Send(HttpMethod method, string url, GeneralUser? caller)
    {
        var response = caller == null
            ? await Client.SendAsync(new HttpRequestMessage(method, url))
            : await Client.SendAsync(CreateAuthenticatedRequest(method, url, caller));
        return new Answer(response.StatusCode,
            response.StatusCode == HttpStatusCode.OK
                ? string.Empty
                : Masked(await response.Content.ReadAsStringAsync()));
    }

    private sealed record Seeded(
        GeneralUser Author,
        GeneralUser Stranger,
        Guid PrivateDraftBlog,
        Guid InPrivateDraftBlog,
        Guid InOpenBlog,
        Guid InHiddenPublication,
        IReadOnlyList<(string Owner, Guid CommentId)> OfEveryDiscussion);

    private async Task<Seeded> SeedAsync(bool inSharedDiscussions = false)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = AddUser(dbContext, "cva");
        var stranger = AddUser(dbContext, "cvs");

        var privateDraft = AddBlog(dbContext, author.UserId, ModuleStatus.Draft, DraftVisibility.Private);
        var openBlog = AddBlog(dbContext, author.UserId, ModuleStatus.Active, DraftVisibility.Public);
        var openPublication = AddPublication(dbContext, openBlog.BlogId, author.UserId);
        var hiddenPublication = AddPublication(dbContext, privateDraft.BlogId, author.UserId);

        var inPrivateDraft = AddComment(dbContext, privateDraft.BlogId, author.UserId);
        var inOpenBlog = AddComment(dbContext, openBlog.BlogId, author.UserId);
        var inOpenPublication = AddComment(dbContext, openPublication.PublicationId, author.UserId);
        var inHiddenPublication = AddComment(dbContext, hiddenPublication.PublicationId, author.UserId);
        var shared = new List<(string Owner, Guid CommentId)>();
        if (inSharedDiscussions)
        {
            shared.Add(("forum", AddComment(dbContext, TestConstants.TestTopicId, author.UserId)));
            shared.Add(("games", AddComment(dbContext, TestConstants.TestGameId, author.UserId)));
        }

        await dbContext.SaveChangesAsync();

        return new Seeded(
            Caller(author),
            Caller(stranger),
            privateDraft.BlogId,
            inPrivateDraft,
            inOpenBlog,
            inHiddenPublication,
            [.. shared, ("blogs", inOpenBlog), ("publications", inOpenPublication)]);
    }

    // The comments written into the topic and the game every test shares go, so those read as they
    // did before.
    private async Task DropSharedAsync(Seeded seeded)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        foreach (var (owner, commentId) in seeded.OfEveryDiscussion)
        {
            if (owner is not ("forum" or "games")) continue;
            await dbContext.Database.ExecuteSqlRawAsync(
                """DELETE FROM "Comments" WHERE "CommentId" = {0}""", commentId);
        }
    }

    private static GeneralUser Caller(DbUser user) =>
        new() { UserId = user.UserId, Username = user.Username, Role = UserRole.RegularUser };

    private static DbUser AddUser(DmDbContext dbContext, string prefix)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@commentvisibility.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        return user;
    }

    private static DbBlog AddBlog(DmDbContext dbContext, Guid authorId, ModuleStatus status,
        DraftVisibility draftVisibility)
    {
        var blog = new DbBlog
        {
            BlogId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = authorId,
            Title = "Comment visibility blog",
            Description = "Seeded by CommentDiscussionVisibilityShould",
            Status = status,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = draftVisibility,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        dbContext.Blogs.Add(blog);
        return blog;
    }

    private static DbPublication AddPublication(DmDbContext dbContext, Guid blogId, Guid authorId)
    {
        var now = DateTimeOffset.UtcNow;
        var publication = new DbPublication
        {
            PublicationId = Guid.NewGuid(),
            BlogId = blogId,
            PublicationNumber = 1,
            AuthorId = authorId,
            Title = "Comment visibility entry",
            Content = "Seeded by CommentDiscussionVisibilityShould",
            Preview = "Seeded",
            CreatedUtc = now
        };
        dbContext.Publications.Add(publication);
        return publication;
    }

    private static Guid AddComment(DmDbContext dbContext, Guid entityId, Guid authorId)
    {
        var commentId = Guid.NewGuid();
        dbContext.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = entityId,
            AuthorId = authorId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = $"Comment {commentId:N}"
        });
        return commentId;
    }
}
