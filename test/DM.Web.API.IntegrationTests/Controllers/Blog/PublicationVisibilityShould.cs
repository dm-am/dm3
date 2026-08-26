using System.Net;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

/// <summary>
/// What a publication inside a blog nobody may open answers, end to end.
/// </summary>
/// <remarks>
/// The listing has always asked the blog first: GET /v1/blogs/{id}/publications
/// reads the parent through the gated blog read, so a private draft and a blog
/// still waiting on premoderation hide everything in them. The single read did
/// not: it fetched the row by id and refused only a draft, so a published
/// publication in a hidden blog was handed to anybody, guest included, through
/// GET /v1/publications/{id}.
///
/// Nothing had to be guessed to reach it: GET /v1/users/{username}/best-publication
/// is public and filtered the author's rows by nothing but "published", so it
/// gave a stranger the identifier and the body at once. That is why the profile
/// widget is asserted here beside the read.
///
/// As with the blog premoderation endpoint next door, the assertions are about
/// indistinguishability rather than about a status code: a hidden publication has
/// to answer exactly what a publication that never existed answers, body
/// included. A 403 would confirm the publication is there, which is the half of
/// the leak that refusing the content does not close.
/// </remarks>
public class PublicationVisibilityShould : IntegrationTestBase
{
    public PublicationVisibilityShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The two ways the parent blog is hidden, and the publication that never
    /// existed, answered by one and the same refusal, to a guest and to a signed-in
    /// stranger alike.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnswerOnAPublicationInAHiddenBlogTheWayItAnswersOnNoPublicationAtAll(
        bool signedIn)
    {
        var seeded = await SeedAsync();
        var stranger = signedIn ? await UserAsync(UserRole.RegularUser) : null;

        var onMissing = await Read(Guid.NewGuid(), stranger);
        var inPrivateDraft = await Read(seeded.InPrivateDraftBlog, stranger);
        var inAwaitingApproval = await Read(seeded.InAwaitingApprovalBlog, stranger);
        var inAwaitingEdits = await Read(seeded.InAwaitingEditsBlog, stranger);

        onMissing.Status.Should().Be(HttpStatusCode.NotFound);
        onMissing.Body.Should().Contain("Публикация не найдена");

        inPrivateDraft.Should().Be(onMissing,
            "a publication in a private draft is not the reader's to know about");
        inAwaitingApproval.Should().Be(onMissing,
            "nor is one in a blog still waiting on a verdict");
        inAwaitingEdits.Should().Be(onMissing,
            "nor one in a blog sent back for edits");
    }

    /// <summary>
    /// And the blog's own people still read it: the gate is about who may open the
    /// blog, not about closing the publication.
    /// </summary>
    [Fact]
    public async Task StillGiveTheAuthorTheirOwnPublicationInAHiddenBlog()
    {
        var seeded = await SeedAsync();

        var inPrivateDraft = await Read(seeded.InPrivateDraftBlog, seeded.Author);
        var inAwaitingApproval = await Read(seeded.InAwaitingApprovalBlog, seeded.Author);

        inPrivateDraft.Status.Should().Be(HttpStatusCode.OK);
        inAwaitingApproval.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// An administrator reads past both gates, as they do on the blog itself.
    /// </summary>
    [Fact]
    public async Task StillGiveAnAdministratorAPublicationInAHiddenBlog()
    {
        var seeded = await SeedAsync();
        var admin = CustomWebApplicationFactory.CreateAdminUser();

        var inPrivateDraft = await Read(seeded.InPrivateDraftBlog, admin);
        var inAwaitingApproval = await Read(seeded.InAwaitingApprovalBlog, admin);

        inPrivateDraft.Status.Should().Be(HttpStatusCode.OK);
        inAwaitingApproval.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A publication in a blog anybody can open is unaffected: the refusal is
    /// about visibility and not about the endpoint being shut.
    /// </summary>
    [Fact]
    public async Task StillGiveAStrangerAPublicationInAnOpenBlog()
    {
        var seeded = await SeedAsync();
        var stranger = await UserAsync(UserRole.RegularUser);

        var response = await Read(seeded.InPublicBlog, stranger);

        response.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The profile widget is the reachability half of the same leak: it is public,
    /// it names one publication of an author it is asked about, and the only thing
    /// it used to filter on was "published". An author whose single publication
    /// sits in a hidden blog has nothing to spotlight.
    /// </summary>
    [Fact]
    public async Task NameNoPublicationFromAHiddenBlogInTheProfileWidget()
    {
        var seeded = await SeedAsync();

        var response = await Client.GetAsync($"/v1/users/{seeded.HiddenOnlyAuthorName}/best-publication");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // An empty envelope is written with the property omitted, not with a
        // literal null, so "no publication" is either of those and nothing else.
        var named = doc.RootElement.TryGetProperty("resource", out var resource) &&
                    resource.ValueKind != JsonValueKind.Null;
        named.Should().BeFalse("the only candidate lives in a blog the reader cannot open");
    }

    /// <summary>
    /// And the widget keeps working where it should: the same author's publication
    /// in an open blog is named.
    /// </summary>
    [Fact]
    public async Task StillNameAPublicationFromAnOpenBlogInTheProfileWidget()
    {
        var seeded = await SeedAsync();

        var response = await Client.GetAsync($"/v1/users/{seeded.Author.Username}/best-publication");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var resource = doc.RootElement.GetProperty("resource");
        resource.ValueKind.Should().Be(JsonValueKind.Object);
        resource.GetProperty("id").GetGuid().Should().Be(seeded.InPublicBlog);
    }

    /// <summary>Status and body together: the body is what tells two 404s apart.</summary>
    private sealed record Refusal(HttpStatusCode Status, string Body);

    private async Task<Refusal> Read(Guid publicationId, GeneralUser? caller)
    {
        var url = $"/v1/publications/{publicationId}";
        var response = caller == null
            ? await Client.GetAsync(url)
            : await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, caller));
        return new Refusal(
            response.StatusCode,
            WithoutCorrelation(await response.Content.ReadAsStringAsync()));
    }

    /// <summary>
    /// The refusal document minus its correlation token. traceId is per-request by
    /// design and is the one field two identical refusals are allowed to differ
    /// in, and everything else (status, type, title, detail) has to match, or the
    /// answer tells the reader which of the two cases they hit.
    /// </summary>
    private static string WithoutCorrelation(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return body;
        }

        return string.Join("&", document.RootElement
            .EnumerateObject()
            .Where(p => p.Name != "traceId")
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={p.Value.GetRawText()}"));
    }

    /// <summary>A caller holding the given site-wide role and no role in any blog.</summary>
    private async Task<GeneralUser> UserAsync(UserRole role)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var user = AddUser(dbContext, "pvs");
        await dbContext.SaveChangesAsync();
        return new GeneralUser { UserId = user.UserId, Username = user.Username, Role = role };
    }

    private sealed record SeededPublications(
        GeneralUser Author,
        string HiddenOnlyAuthorName,
        Guid InPrivateDraftBlog,
        Guid InAwaitingApprovalBlog,
        Guid InAwaitingEditsBlog,
        Guid InPublicBlog);

    /// <summary>
    /// One author with four blogs differing only in what hides them, a published
    /// publication in each, and a second author whose single publication sits in a
    /// hidden blog: the profile widget needs an author with nothing else to name.
    /// Fresh identifiers every time: the fixture database is shared and its
    /// usernames and public ids are uniquely indexed.
    /// </summary>
    private async Task<SeededPublications> SeedAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = AddUser(dbContext, "pva");

        // MentorId is left null on the premoderated blogs: that is the state a
        // newbie's blog is created in.
        var privateDraft = AddBlog(dbContext, author.UserId,
            ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private);
        var awaitingApproval = AddBlog(dbContext, author.UserId,
            ModuleStatus.Active, PremoderationStatus.AwaitingApproval);
        var awaitingEdits = AddBlog(dbContext, author.UserId,
            ModuleStatus.Active, PremoderationStatus.AwaitingEdits);
        var publicBlog = AddBlog(dbContext, author.UserId,
            ModuleStatus.Active, PremoderationStatus.Approved);

        var inPrivateDraft = AddPublication(dbContext, privateDraft.BlogId, author.UserId);
        var inAwaitingApproval = AddPublication(dbContext, awaitingApproval.BlogId, author.UserId);
        var inAwaitingEdits = AddPublication(dbContext, awaitingEdits.BlogId, author.UserId);
        var inPublicBlog = AddPublication(dbContext, publicBlog.BlogId, author.UserId);

        var hiddenOnlyAuthor = AddUser(dbContext, "pvh");
        var hiddenOnlyBlog = AddBlog(dbContext, hiddenOnlyAuthor.UserId,
            ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private);
        AddPublication(dbContext, hiddenOnlyBlog.BlogId, hiddenOnlyAuthor.UserId);

        await dbContext.SaveChangesAsync();

        return new SeededPublications(
            new GeneralUser
            {
                UserId = author.UserId,
                Username = author.Username,
                Role = UserRole.RegularUser
            },
            hiddenOnlyAuthor.Username,
            inPrivateDraft.PublicationId,
            inAwaitingApproval.PublicationId,
            inAwaitingEdits.PublicationId,
            inPublicBlog.PublicationId);
    }

    private static DbUser AddUser(DmDbContext dbContext, string prefix)
    {
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@publicationvisibility.example",
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
            Title = "Publication visibility blog",
            Description = "Seeded by PublicationVisibilityShould",
            Status = status,
            PremoderationStatus = premoderationStatus,
            DraftVisibility = draftVisibility,
            CommentsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        dbContext.Blogs.Add(blog);
        return blog;
    }

    /// <summary>
    /// Published, not a draft: what is under test is the parent blog and nothing
    /// else, so the publication's own gate has to be out of the way.
    /// </summary>
    private static DbPublication AddPublication(DmDbContext dbContext, Guid blogId, Guid authorId)
    {
        var now = DateTimeOffset.UtcNow;
        var publication = new DbPublication
        {
            PublicationId = Guid.NewGuid(),
            BlogId = blogId,
            PublicationNumber = 1,
            AuthorId = authorId,
            Title = "Publication visibility entry",
            Content = "Seeded by PublicationVisibilityShould",
            Preview = "Seeded",
            CommentsEnabled = true,
            IsPublished = true,
            PublishedUtc = now,
            CreatedUtc = now,
            IsRemoved = false
        };
        dbContext.Publications.Add(publication);
        return publication;
    }
}
