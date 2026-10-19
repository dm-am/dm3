using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

/// <summary>
/// What a stranger learns about a blog they may not see: from POST /v1/blogs/{id}/premoderation and from the
/// plain reads by alias and by owner.
/// </summary>
/// <remarks>
/// Every hidden blog answers exactly what an unclaimed alias answers; the verdict half is asserted beside it,
/// and a visible blog still refuses an action with a 403.
/// </remarks>
public class BlogPremoderationVisibilityShould : IntegrationTestBase
{
    public BlogPremoderationVisibilityShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The three ways a blog is hidden from a stranger, plus the alias nobody has
    /// taken, answered by one and the same refusal.
    /// </summary>
    [Fact]
    public async Task AnswerAStrangerOnEveryHiddenBlogTheWayItAnswersOnNoBlogAtAll()
    {
        var seeded = await SeedAsync();
        var stranger = await UserAsync(UserRole.RegularUser);

        var onMissing = await Submit(Guid.NewGuid().ToString("N")[..10], stranger);
        var onPrivateDraft = await Submit(seeded.PrivateDraftPublicId, stranger);
        var onAwaitingEdits = await Submit(seeded.AwaitingEditsPublicId, stranger);
        var onAwaitingApproval = await Submit(seeded.AwaitingApprovalPublicId, stranger);

        onMissing.Should().Be(HttpStatusCode.NotFound);
        onPrivateDraft.Should().Be(onMissing, "a private draft is not the stranger's to know about");
        onAwaitingEdits.Should().Be(onMissing, "the status of a premoderated blog is not the stranger's to know");
        onAwaitingApproval.Should().Be(onMissing, "and neither is the other premoderation status");
    }

    /// <summary>
    /// The owner's own move keeps working on the blog only the owner can see —
    /// which is the whole reason the endpoint was opened past Mentor+.
    /// </summary>
    [Fact]
    public async Task KeepTheOwnersMoveOpenOnTheBlogOnlyTheOwnerCanSee()
    {
        var seeded = await SeedAsync();

        var status = await Submit(seeded.AwaitingEditsPublicId, seeded.Owner);

        status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The verdict reads past visibility: a blog in AwaitingEdits has no curator recorded.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task LetAVerdictThroughOnABlogTheJudgeHoldsNoRoleIn(UserRole role)
    {
        var seeded = await SeedAsync();
        var judge = await UserAsync(role);

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post, Url(seeded.AwaitingApprovalPublicId), judge);
        request.Content = JsonContent.Create(new { transition = "SetApproved" });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The refusal is about what the caller may see and not about the endpoint
    /// being shut: a blog anybody can open answers on its own terms, and the
    /// illegal move on it is the 400 it always was.
    /// </summary>
    [Fact]
    public async Task StillRefuseAnIllegalMoveOnAPubliclyVisibleBlogWithBadRequest()
    {
        var seeded = await SeedAsync();
        var stranger = await UserAsync(UserRole.RegularUser);

        var status = await Submit(seeded.PublicPublicId, stranger);

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The plain read of a blog by its alias, which is the same oracle reached
    /// without any transition at all — and reachable by a guest.
    /// </summary>
    /// <remarks>Whole bodies, not statuses: a different title or problem type would name the blog as loudly.</remarks>
    [Fact]
    public async Task AnswerAReadOfAHiddenAliasTheWayItAnswersAnUnclaimedOne()
    {
        var seeded = await SeedAsync();

        var onMissing = await Client.GetAsync(ReadUrl(Guid.NewGuid().ToString("N")[..10]));
        var onPrivateDraft = await Client.GetAsync(ReadUrl(seeded.PrivateDraftPublicId));
        var onAwaitingEdits = await Client.GetAsync(ReadUrl(seeded.AwaitingEditsPublicId));
        var onAwaitingApproval = await Client.GetAsync(ReadUrl(seeded.AwaitingApprovalPublicId));

        onMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var expected = await BodyWithoutTrace(onMissing);

        foreach (var hidden in new[] { onPrivateDraft, onAwaitingEdits, onAwaitingApproval })
        {
            hidden.StatusCode.Should().Be(onMissing.StatusCode);
            (await BodyWithoutTrace(hidden)).Should().Be(expected);
        }
    }

    /// <summary>
    /// The same read, by somebody with an account and no role in the blog: the
    /// authenticated surface is the wider one, since every blog-scoped route
    /// resolves its alias through this call.
    /// </summary>
    [Fact]
    public async Task AnswerAStrangerReadingAHiddenAliasTheWayItAnswersAnUnclaimedOne()
    {
        var seeded = await SeedAsync();
        var stranger = await UserAsync(UserRole.RegularUser);

        var onMissing = await Read(Guid.NewGuid().ToString("N")[..10], stranger);
        var onPrivateDraft = await Read(seeded.PrivateDraftPublicId, stranger);
        var onAwaitingApproval = await Read(seeded.AwaitingApprovalPublicId, stranger);

        onMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var expected = await BodyWithoutTrace(onMissing);

        (await BodyWithoutTrace(onPrivateDraft)).Should().Be(expected);
        (await BodyWithoutTrace(onAwaitingApproval)).Should().Be(expected);
    }

    /// <summary>
    /// The owner route asks by login rather than by alias, and answers on a
    /// hidden blog exactly as on a user who keeps none.
    /// </summary>
    /// <remarks>Compared with each caller's own login masked out; status, type and wording match.</remarks>
    [Fact]
    public async Task AnswerAReadByOwnerTheWayItAnswersOnAnOwnerWithNoBlog()
    {
        var hiddenOwner = await OwnerOfOneBlogAsync(
            PremoderationStatus.Approved, DraftVisibility.Private);
        var premoderatedOwner = await OwnerOfOneBlogAsync(
            PremoderationStatus.AwaitingApproval, DraftVisibility.Public);
        var blogless = await UserAsync(UserRole.RegularUser);

        var onBlogless = await Client.GetAsync($"/v1/blogs/owner/{blogless.Username}");
        var onHidden = await Client.GetAsync($"/v1/blogs/owner/{hiddenOwner.Username}");
        var onPremoderated = await Client.GetAsync($"/v1/blogs/owner/{premoderatedOwner.Username}");

        onBlogless.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var expected = await BodyWithoutTrace(onBlogless, blogless.Username);

        onHidden.StatusCode.Should().Be(onBlogless.StatusCode);
        (await BodyWithoutTrace(onHidden, hiddenOwner.Username)).Should().Be(expected);
        onPremoderated.StatusCode.Should().Be(onBlogless.StatusCode);
        (await BodyWithoutTrace(onPremoderated, premoderatedOwner.Username)).Should().Be(expected);
    }

    /// <summary>
    /// What must not change: a blog the caller can see refuses an action the
    /// caller has no right to with a 403, which is what a 403 is for.
    /// </summary>
    /// <remarks>The rule above is about visibility alone, not "every refusal is a 404".</remarks>
    [Fact]
    public async Task StillRefuseEditingSomebodyElsesVisibleBlogWithForbidden()
    {
        var seeded = await SeedAsync();
        var stranger = await UserAsync(UserRole.RegularUser);

        var request = CreateAuthenticatedRequest(
            HttpMethod.Patch, ReadUrl(seeded.PublicPublicId), stranger);
        request.Content = JsonContent.Create(new { title = "Чужой блог, чужая правка" });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Url(string publicId) => $"/v1/blogs/{publicId}/premoderation";

    private static string ReadUrl(string publicId) => $"/v1/blogs/{publicId}";

    private async Task<HttpResponseMessage> Read(string publicId, GeneralUser caller) =>
        await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, ReadUrl(publicId), caller));

    /// <summary>
    /// The response body with the one field that differs between any two
    /// requests removed, and optionally the caller's login masked.
    /// </summary>
    private static async Task<string> BodyWithoutTrace(HttpResponseMessage response, string? login = null)
    {
        var payload = await response.Content.ReadAsStringAsync();
        var body = JsonNode.Parse(payload)!.AsObject();
        body.Remove("traceId");
        var text = body.ToJsonString();
        return login == null ? text : text.Replace(login, "{login}");
    }

    private async Task<HttpStatusCode> Submit(string publicId, GeneralUser caller)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, Url(publicId), caller);
        request.Content = JsonContent.Create(new { transition = "SubmitForApproval" });
        var response = await Client.SendAsync(request);
        return response.StatusCode;
    }

    /// <summary>A caller holding the given site-wide role and no role in any blog.</summary>
    private async Task<GeneralUser> UserAsync(UserRole role)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var user = AddUser(dbContext, "bpr");
        await dbContext.SaveChangesAsync();
        return new GeneralUser { UserId = user.UserId, Username = user.Username, Role = role };
    }

    /// <summary>
    /// A fresh user owning exactly one blog: the owner route answers with the first blog it finds.
    /// </summary>
    private async Task<GeneralUser> OwnerOfOneBlogAsync(
        PremoderationStatus premoderationStatus, DraftVisibility draftVisibility)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var owner = AddUser(dbContext, "bph");
        // The draft visibility hides a blog only while it is a draft.
        var status = draftVisibility == DraftVisibility.Private ? ModuleStatus.Draft : ModuleStatus.Active;
        AddBlog(dbContext, owner.UserId, status, premoderationStatus, draftVisibility);
        await dbContext.SaveChangesAsync();

        return new GeneralUser
        {
            UserId = owner.UserId,
            Username = owner.Username,
            Role = UserRole.RegularUser
        };
    }

    private sealed record SeededBlogs(
        GeneralUser Owner,
        string PrivateDraftPublicId,
        string AwaitingEditsPublicId,
        string AwaitingApprovalPublicId,
        string PublicPublicId);

    /// <summary>
    /// Four blogs of one owner, differing only in what hides them, under fresh identifiers.
    /// </summary>
    private async Task<SeededBlogs> SeedAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var owner = AddUser(dbContext, "bpo");

        // MentorId is left null on both premoderated blogs: that is the state a
        // newbie's blog is created in, and it is what makes the verdict half read
        // past visibility instead of through the blog's own people.
        var privateDraft = AddBlog(dbContext, owner.UserId,
            ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private);
        var awaitingEdits = AddBlog(dbContext, owner.UserId,
            ModuleStatus.Active, PremoderationStatus.AwaitingEdits);
        var awaitingApproval = AddBlog(dbContext, owner.UserId,
            ModuleStatus.Active, PremoderationStatus.AwaitingApproval);
        var publicBlog = AddBlog(dbContext, owner.UserId,
            ModuleStatus.Active, PremoderationStatus.Approved);

        await dbContext.SaveChangesAsync();

        return new SeededBlogs(
            new GeneralUser
            {
                UserId = owner.UserId,
                Username = owner.Username,
                Role = UserRole.RegularUser
            },
            privateDraft.PublicId,
            awaitingEdits.PublicId,
            awaitingApproval.PublicId,
            publicBlog.PublicId);
    }

    private static DbUser AddUser(DmDbContext dbContext, string prefix)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@blogpremoderation.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        return user;
    }

    private static DbBlog AddBlog(
        DmDbContext dbContext,
        Guid authorId,
        ModuleStatus status,
        PremoderationStatus premoderationStatus,
        DraftVisibility draftVisibility = DraftVisibility.Public)
    {
        var blog = new DbBlog
        {
            BlogId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = authorId,
            Title = "Premoderation visibility blog",
            Description = "Seeded by BlogPremoderationVisibilityShould",
            Status = status,
            PremoderationStatus = premoderationStatus,
            DraftVisibility = draftVisibility,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        dbContext.Blogs.Add(blog);
        return blog;
    }
}
