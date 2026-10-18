using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBlogBlacklist = DM.Infrastructure.Persistence.Entities.Blog.BlogBlacklist;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameBlacklist = DM.Infrastructure.Persistence.Entities.Game.Links.GameBlacklist;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The page offers the comment form by <c>viewer.canComment</c> of the game, the
/// blog, the publication and the topic, and the flag is false exactly when the
/// comment it stands for is refused.
/// </summary>
/// <remarks>
/// The owner, 05.10: what the user may not do is not offered, rather than
/// refused after it was typed. The pages used to work the rule out on their own
/// from the mode of the discussion and the blacklist, could not see a ban at
/// all, and gave the banned reader a form and a refusal under it.
///
/// Every row reads the flag the way the page reads it and then sends the comment
/// the form would send, as the same viewer, so the two answers are compared
/// rather than each taken on trust. The expected value is asserted too: a flag
/// and a write that both went wrong the same way would agree with each other.
///
/// Each case makes its own record and its own people and takes everything but
/// the people back out.
/// </remarks>
public class CanCommentFlagShould : IntegrationTestBase
{
    public CanCommentFlagShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>The record whose discussion is asked about.</summary>
    public enum Surface
    {
        Game,
        Blog,
        Publication,
        Topic
    }

    /// <summary>Who reads the record and writes the comment.</summary>
    public enum Who
    {
        Guest,

        /// <summary>A user with no part in the record.</summary>
        Stranger,

        /// <summary>The master of the game, the owner of the blog, the author of the topic.</summary>
        Owner,

        /// <summary>A reader (subscriber) of the game.</summary>
        Reader
    }

    /// <summary>What the record is like.</summary>
    [Flags]
    public enum State
    {
        Open = 0,
        ReadOnly = 1,
        Draft = 4,
        Closed = 8,
        Blacklisted = 16
    }

    private sealed record Case(Surface Surface, Who Who, State State, bool Banned, bool Expected);

    private static readonly Dictionary<string, Case> Table = new()
    {
        // The game: the mode of the discussion with the roles, the game's
        // blacklist, and the ban, which leaves one's own game open.
        ["a stranger in a public game"] = new(Surface.Game, Who.Stranger, State.Open, false, true),
        ["a guest in a public game"] = new(Surface.Game, Who.Guest, State.Open, false, false),
        ["a stranger in a read-only game"] = new(Surface.Game, Who.Stranger, State.ReadOnly, false, false),
        ["a reader in a read-only game"] = new(Surface.Game, Who.Reader, State.ReadOnly, false, true),
        ["the master in a read-only game"] = new(Surface.Game, Who.Owner, State.ReadOnly, false, true),
        ["a blacklisted stranger in a public game"] = new(Surface.Game, Who.Stranger, State.Blacklisted, false, false),
        ["a banned stranger in a public game"] = new(Surface.Game, Who.Stranger, State.Open, true, false),
        ["a banned reader in a public game"] = new(Surface.Game, Who.Reader, State.Open, true, false),
        ["the banned master in his game"] = new(Surface.Game, Who.Owner, State.Open, true, true),

        // The blog: its blacklist and the ban outside one's own blog. Comments
        // have no switch, so nothing else closes it.
        ["a stranger in an open blog"] = new(Surface.Blog, Who.Stranger, State.Open, false, true),
        ["a guest in an open blog"] = new(Surface.Blog, Who.Guest, State.Open, false, false),
        ["a blacklisted stranger in a blog"] = new(Surface.Blog, Who.Stranger, State.Blacklisted, false, false),
        ["a banned stranger in a blog"] = new(Surface.Blog, Who.Stranger, State.Open, true, false),
        ["the banned owner in his blog"] = new(Surface.Blog, Who.Owner, State.Open, true, true),

        // The publication: published, the blog's blacklist, the ban outside
        // one's own blog - three refusals in three places.
        ["a stranger under a publication"] = new(Surface.Publication, Who.Stranger, State.Open, false, true),
        ["a guest under a publication"] = new(Surface.Publication, Who.Guest, State.Open, false, false),
        ["the author under his draft"] = new(Surface.Publication, Who.Owner, State.Draft, false, false),
        ["a stranger blacklisted by the blog under its publication"] =
            new(Surface.Publication, Who.Stranger, State.Blacklisted, false, false),
        ["a banned stranger under a publication"] = new(Surface.Publication, Who.Stranger, State.Open, true, false),
        ["the banned owner under a publication of his blog"] =
            new(Surface.Publication, Who.Owner, State.Open, true, true),

        // The topic: open or closed, and the ban, with no own-space exemption.
        ["a stranger under an open topic"] = new(Surface.Topic, Who.Stranger, State.Open, false, true),
        ["a guest under an open topic"] = new(Surface.Topic, Who.Guest, State.Open, false, false),
        ["the author under his closed topic"] = new(Surface.Topic, Who.Owner, State.Closed, false, false),
        ["a banned stranger under an open topic"] = new(Surface.Topic, Who.Stranger, State.Open, true, false),
        ["the banned author under his own topic"] = new(Surface.Topic, Who.Owner, State.Open, true, false),
    };

    public static TheoryData<string> Cases() => new(Table.Keys);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OfferTheCommentExactlyWhenTheCommentIsTaken(string name)
    {
        var @case = Table[name];
        var seed = await SeedAsync(@case);
        try
        {
            var viewer = @case.Who switch
            {
                Who.Guest => null,
                Who.Owner => seed.Owner,
                Who.Reader => seed.Reader,
                _ => seed.Stranger
            };

            var flag = await FlagAsync(viewer, @case.Banned, ReadUrl(@case.Surface, seed));
            var written = await Send(viewer, @case.Banned, HttpMethod.Post, WriteUrl(@case.Surface, seed),
                new { text = "Комментарий, который предлагает форма" });
            var body = await written.Content.ReadAsStringAsync();

            flag.Should().Be(@case.Expected, name);
            written.IsSuccessStatusCode.Should().Be(flag, $"the flag said {flag} and the comment answered: {body}");
            if (!flag)
            {
                // Refused for who the viewer is, not lost on a record the
                // case failed to make.
                written.StatusCode.Should().Be(
                    viewer is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, body);
            }
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// Closing a topic is a PATCH, and the page puts the topic the PATCH returns
    /// in place of the one it holds: that answer has to carry the flag the
    /// closing leaves, or the form stays under a topic just closed.
    /// </summary>
    [Fact]
    public async Task AnswerTheClosingOfATopicWithTheFlagItLeaves()
    {
        var seed = await SeedAsync(new Case(Surface.Topic, Who.Owner, State.Open, false, true));
        try
        {
            var closed = await Send(seed.Owner, false, HttpMethod.Patch, $"/v1/topics/{seed.TopicId}",
                new { isClosed = true });
            var closedBody = await closed.Content.ReadAsStringAsync();
            closed.StatusCode.Should().Be(HttpStatusCode.OK, closedBody);
            CanComment(closedBody).Should().BeFalse();

            var reopened = await Send(seed.Owner, false, HttpMethod.Patch, $"/v1/topics/{seed.TopicId}",
                new { isClosed = false });
            var reopenedBody = await reopened.Content.ReadAsStringAsync();
            reopened.StatusCode.Should().Be(HttpStatusCode.OK, reopenedBody);
            CanComment(reopenedBody).Should().BeTrue();
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// The topic's discussion endpoint answers whether the reader may add a
    /// comment, and used to read it off the session alone: the reader of a
    /// closed topic and a banned one were told they could.
    /// </summary>
    [Theory]
    [InlineData(State.Open, false, true)]
    [InlineData(State.Closed, false, false)]
    [InlineData(State.Open, true, false)]
    public async Task TellTheTruthInTheDiscussionOfATopic(State state, bool banned, bool expected)
    {
        var seed = await SeedAsync(new Case(Surface.Topic, Who.Stranger, state, banned, expected));
        try
        {
            var response = await Send(seed.Stranger, banned, HttpMethod.Get, $"/v1/topics/{seed.TopicId}/discussion");
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);

            using var document = JsonDocument.Parse(body);
            document.RootElement.GetProperty("canComment").GetBoolean().Should().Be(expected);
            (await FlagAsync(seed.Stranger, banned, ReadUrl(Surface.Topic, seed))).Should().Be(expected);
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// A list offers no comment form, and the list-shaped reads of a blog fill
    /// the viewer's standing differently from the read the comment is refused by,
    /// so a list carries no flag at all rather than one that could disagree.
    /// </summary>
    [Fact]
    public async Task LeaveTheFlagOffTheLists()
    {
        var blog = await SeedAsync(new Case(Surface.Publication, Who.Stranger, State.Open, false, true));
        var topic = await SeedAsync(new Case(Surface.Topic, Who.Stranger, State.Open, false, true));
        try
        {
            var blogs = (await GetAsync(blog.Stranger, "/v1/blogs?take=200")).GetProperty("resources")
                .EnumerateArray().ToArray();
            blogs.Should().Contain(b => b.GetProperty("id").GetGuid() == blog.BlogId);
            blogs.Should().AllSatisfy(b => b.TryGetProperty("viewer", out _).Should().BeFalse());

            var feed = (await GetAsync(blog.Stranger, $"/v1/blogs/{blog.BlogId}/publications"))
                .GetProperty("resources").EnumerateArray().ToArray();
            feed.Should().Contain(p => p.GetProperty("id").GetGuid() == blog.PublicationId);
            feed.Should().AllSatisfy(p => p.TryGetProperty("viewer", out _).Should().BeFalse());

            var topics = (await GetAsync(topic.Stranger, $"/v1/boards/{topic.BoardId}/topics"))
                .GetProperty("resources").EnumerateArray().ToArray();
            topics.Should().Contain(t => t.GetProperty("id").GetGuid() == topic.TopicId);
            topics.Should().AllSatisfy(t => t.TryGetProperty("viewer", out _).Should().BeFalse());

            // ...and the read of one carries it, which is what the page is offered by.
            (await GetAsync(blog.Stranger, $"/v1/blogs/{blog.BlogId}"))
                .GetProperty("resource").TryGetProperty("viewer", out _).Should().BeTrue();
        }
        finally
        {
            await DropAsync(blog);
            await DropAsync(topic);
        }
    }

    // ═══ SEEDING ═══

    /// <summary>
    /// The records of a case and the people around them. Everything is made
    /// whatever the surface, so one drop takes any case back out.
    /// </summary>
    private sealed record Seed(GeneralUser Owner, GeneralUser Stranger, GeneralUser Reader)
    {
        public Guid GameId { get; } = Guid.NewGuid();
        public Guid BlogId { get; } = Guid.NewGuid();
        public Guid PublicationId { get; } = Guid.NewGuid();
        public Guid BoardId { get; } = Guid.NewGuid();
        public Guid TopicId { get; } = Guid.NewGuid();
        public string BoardAlias => $"ccf-{BoardId:N}"[..20];
    }

    private async Task<Seed> SeedAsync(Case @case)
    {
        var seed = new Seed(
            await SeedUserAsync("ccf-own"),
            await SeedUserAsync("ccf-str"),
            await SeedUserAsync("ccf-rdr"));
        var now = DateTimeOffset.UtcNow;
        var state = @case.State;

        await using var db = DatabaseFixture.CreateDbContext();

        db.Set<DbGame>().Add(new DbGame
        {
            GameId = seed.GameId,
            PublicId = seed.GameId.ToString("N")[..10],
            Title = $"Discussion flag game {seed.GameId:N}",
            MasterId = seed.Owner.UserId,
            CreatedUtc = now.AddDays(-2),
            ActivatedUtc = now.AddDays(-1),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = state.HasFlag(State.ReadOnly) ? CommentsAccessMode.Readonly : CommentsAccessMode.Public,
            IsRemoved = false
        });
        // The reads of a game fail loudly without its sheet.
        db.AddRange(GameSheetTestHelper.Sheets([seed.GameId]));
        db.Set<DbSubscription>().Add(new DbSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = seed.Reader.UserId,
            TargetType = SubscriptionTargetType.Game,
            TargetId = seed.GameId,
            Settings = SubscriptionSettings.InApp,
            CreatedUtc = now
        });

        db.Set<DbBlog>().Add(new DbBlog
        {
            BlogId = seed.BlogId,
            PublicId = seed.BlogId.ToString("N")[..10],
            AuthorId = seed.Owner.UserId,
            Title = $"Discussion flag blog {seed.BlogId:N}",
            Description = string.Empty,
            CreatedUtc = now.AddDays(-2),
            ActivatedUtc = now.AddDays(-1),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            IsRemoved = false
        });
        db.Set<DbPublication>().Add(new DbPublication
        {
            PublicationId = seed.PublicationId,
            BlogId = seed.BlogId,
            PublicationNumber = 1,
            AuthorId = seed.Owner.UserId,
            Title = "Публикация для формы комментария",
            Content = "Текст",
            Preview = "Текст",
            IsPublished = !state.HasFlag(State.Draft),
            PublishedUtc = state.HasFlag(State.Draft) ? null : now,
            CreatedUtc = now,
            IsRemoved = false
        });

        if (state.HasFlag(State.Blacklisted))
        {
            db.Set<DbGameBlacklist>().Add(new DbGameBlacklist
            {
                EntryId = Guid.NewGuid(),
                GameId = seed.GameId,
                BlockedUserId = seed.Stranger.UserId,
                BlockedByUserId = seed.Owner.UserId,
                CreatedUtc = now
            });
            db.Set<DbBlogBlacklist>().Add(new DbBlogBlacklist
            {
                EntryId = Guid.NewGuid(),
                BlogId = seed.BlogId,
                BlockedUserId = seed.Stranger.UserId,
                BlockedByUserId = seed.Owner.UserId,
                CreatedUtc = now
            });
        }

        db.Set<DbBoard>().Add(new DbBoard
        {
            BoardId = seed.BoardId,
            Title = $"Discussion flag board {seed.BoardId:N}",
            Alias = seed.BoardAlias,
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.RegularUser
        });
        db.Set<DbTopic>().Add(new DbTopic
        {
            TopicId = seed.TopicId,
            BoardId = seed.BoardId,
            AuthorId = seed.Owner.UserId,
            Title = "Топик для формы комментария",
            Text = "Текст топика",
            TopicNumber = 1,
            CreatedUtc = now.AddHours(-1),
            IsRemoved = false,
            IsAttached = false,
            IsClosed = state.HasFlag(State.Closed),
            ClosedByAuthor = state.HasFlag(State.Closed)
        });

        await db.SaveChangesAsync();
        return seed;
    }

    private async Task<GeneralUser> SeedUserAsync(string prefix)
    {
        var userId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@cancommentflag.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new GeneralUser { UserId = userId, Username = user.Username, Role = UserRole.RegularUser };
    }

    /// <summary>
    /// Everything the case made, and everything its comments wrote, but the
    /// people: their rows stay, the way the neighbouring suites leave theirs.
    /// </summary>
    private async Task DropAsync(Seed seed)
    {
        var records = new[] { seed.GameId, seed.BlogId, seed.PublicationId, seed.TopicId };
        var people = new[] { seed.Owner.UserId, seed.Stranger.UserId, seed.Reader.UserId };
        await using var db = DatabaseFixture.CreateDbContext();

        // The last comment is a reference from the record to the comment.
        await db.Set<DbGame>().IgnoreQueryFilters().Where(g => g.GameId == seed.GameId)
            .ExecuteUpdateAsync(g => g.SetProperty(x => x.LastCommentId, (Guid?)null));
        await db.Set<DbBlog>().IgnoreQueryFilters().Where(b => b.BlogId == seed.BlogId)
            .ExecuteUpdateAsync(b => b.SetProperty(x => x.LastCommentId, (Guid?)null));
        await db.Set<DbPublication>().IgnoreQueryFilters().Where(p => p.PublicationId == seed.PublicationId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastCommentId, (Guid?)null));
        await db.Set<DbTopic>().IgnoreQueryFilters().Where(t => t.TopicId == seed.TopicId)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.LastCommentId, (Guid?)null));
        await db.Set<DbBoard>().Where(b => b.BoardId == seed.BoardId)
            .ExecuteUpdateAsync(b => b.SetProperty(x => x.LastTopicId, (Guid?)null));

        await db.Comments.IgnoreQueryFilters().Where(c => records.Contains(c.EntityId)).ExecuteDeleteAsync();
        await db.UnreadCounters.Where(c => records.Contains(c.EntityId)).ExecuteDeleteAsync();
        await db.Set<DbSubscription>()
            .Where(s => records.Contains(s.TargetId) || people.Contains(s.SubscriberId)).ExecuteDeleteAsync();
        await db.Set<DbGameBlacklist>().Where(b => b.GameId == seed.GameId).ExecuteDeleteAsync();
        await db.Set<DbBlogBlacklist>().Where(b => b.BlogId == seed.BlogId).ExecuteDeleteAsync();
        await db.Set<DbPublication>().IgnoreQueryFilters().Where(p => p.PublicationId == seed.PublicationId)
            .ExecuteDeleteAsync();
        await db.Set<DbTopic>().IgnoreQueryFilters().Where(t => t.BoardId == seed.BoardId).ExecuteDeleteAsync();
        await db.Set<DbBoard>().Where(b => b.BoardId == seed.BoardId).ExecuteDeleteAsync();
        await db.Set<DbBlog>().IgnoreQueryFilters().Where(b => b.BlogId == seed.BlogId).ExecuteDeleteAsync();
        // The sheet goes with the game (cascade).
        await db.Set<DbGame>().IgnoreQueryFilters().Where(g => g.GameId == seed.GameId).ExecuteDeleteAsync();
    }

    // ═══ REQUESTS ═══

    private static string ReadUrl(Surface surface, Seed seed) => surface switch
    {
        Surface.Game => $"/v1/games/{seed.GameId}/details",
        Surface.Blog => $"/v1/blogs/{seed.BlogId}",
        Surface.Publication => $"/v1/publications/{seed.PublicationId}",
        _ => $"/v1/topics/{seed.TopicId}"
    };

    private static string WriteUrl(Surface surface, Seed seed) => surface switch
    {
        Surface.Game => $"/v1/games/{seed.GameId}/comments",
        Surface.Blog => $"/v1/blogs/{seed.BlogId}/comments",
        Surface.Publication => $"/v1/publications/{seed.PublicationId}/comments",
        _ => $"/v1/topics/{seed.TopicId}/comments"
    };

    /// <summary>A request as the viewer, under an ordinary ban when asked; a guest sends none.</summary>
    private Task<HttpResponseMessage> Send(
        GeneralUser? user, bool banned, HttpMethod method, string url, object? body = null)
    {
        var request = user is null
            ? new HttpRequestMessage(method, url)
            : CreateAuthenticatedRequest(method, url, user);
        if (user is not null && banned)
        {
            request.Headers.Add(TestAuthenticationMiddleware.TestUserAccessPolicyHeader,
                AccessPolicy.DemocraticBan.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return Client.SendAsync(request);
    }

    /// <summary>The flag the page reads, off the record it reads.</summary>
    private async Task<bool> FlagAsync(GeneralUser? user, bool banned, string url)
    {
        var response = await Send(user, banned, HttpMethod.Get, url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return CanComment(body);
    }

    private static bool CanComment(string envelope)
    {
        using var document = JsonDocument.Parse(envelope);
        return document.RootElement.GetProperty("resource").GetProperty("viewer").GetProperty("canComment")
            .GetBoolean();
    }

    private async Task<JsonElement> GetAsync(GeneralUser user, string url)
    {
        var response = await Send(user, false, HttpMethod.Get, url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
