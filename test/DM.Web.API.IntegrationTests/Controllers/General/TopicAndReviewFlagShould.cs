using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The forum offers "Создать топик" by <c>board.viewer.canCreateTopic</c>, the
/// game its review form by <c>game.viewer.canReview</c>, and a post the form of
/// its rating by <c>post.viewer.canReview</c>. Each flag is false exactly when
/// the write it stands for is refused.
/// </summary>
/// <remarks>
/// The owner, 05.10: what the user may not do is not offered, rather than
/// refused after it was typed. The forum gave every member the button in the
/// news and the contests and refused the topic after, the reviews page gave the
/// form to a newbie and to whoever had no post in the game, and a post opened
/// its rating to a reader who had rated in the same game two days before.
///
/// Every row reads the flag where the page reads it and then sends what the
/// form would send, as the same viewer, so the two answers are compared rather
/// than each taken on trust. The expected value is asserted too: a flag and a
/// write that both went wrong the same way would agree with each other.
///
/// Each case makes its own records and its own people and takes everything but
/// the people back out.
/// </remarks>
public class TopicAndReviewFlagShould : IntegrationTestBase
{
    public TopicAndReviewFlagShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // ═══ "СОЗДАТЬ ТОПИК" ═══

    /// <summary>Who stands on the board.</summary>
    public enum Visitor
    {
        Guest,
        Member,
        BannedMember,
        Moderator
    }

    private sealed record BoardCase(Visitor Visitor, bool ModeratorsOnly, bool Expected, bool ExpectedAfterSignIn);

    private static readonly Dictionary<string, BoardCase> BoardTable = new()
    {
        ["a member on an open board"] = new(Visitor.Member, false, true, false),
        ["a member on the board of the moderation"] = new(Visitor.Member, true, false, false),
        ["a moderator on the board of the moderation"] = new(Visitor.Moderator, true, true, false),
        ["a banned member on an open board"] = new(Visitor.BannedMember, false, false, false),
        // Signing in is the way to a topic only where a member may open one.
        ["a guest on an open board"] = new(Visitor.Guest, false, false, true),
        ["a guest on the board of the moderation"] = new(Visitor.Guest, true, false, false),
    };

    public static TheoryData<string> BoardCases() => new(BoardTable.Keys);

    [Theory]
    [MemberData(nameof(BoardCases))]
    public async Task OfferTheTopicExactlyWhenTheTopicIsTaken(string name)
    {
        var @case = BoardTable[name];
        var seed = await SeedAsync();
        try
        {
            var boardId = @case.ModeratorsOnly ? seed.ModerationBoardId : seed.OpenBoardId;
            var (viewer, banned) = @case.Visitor switch
            {
                Visitor.Guest => (null, false),
                Visitor.Moderator => (seed.Moderator, false),
                Visitor.BannedMember => (seed.Stranger, true),
                _ => ((GeneralUser?)seed.Stranger, false)
            };

            var board = (await ReadAsync(viewer, banned, $"/v1/boards/{boardId}")).GetProperty("viewer");
            var flag = board.GetProperty("canCreateTopic").GetBoolean();
            var written = await Send(viewer, banned, HttpMethod.Post, $"/v1/boards/{boardId}/topics",
                new { title = "Топик, который предлагает кнопка", text = "Текст топика" });
            var body = await written.Content.ReadAsStringAsync();

            flag.Should().Be(@case.Expected, name);
            written.IsSuccessStatusCode.Should().Be(flag, $"the flag said {flag} and the topic answered: {body}");
            if (!flag)
            {
                // Refused for who the viewer is, not lost on a board the case
                // failed to make.
                written.StatusCode.Should().Be(
                    viewer is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, body);
            }

            board.GetProperty("canCreateTopicAfterSignIn").GetBoolean().Should().Be(@case.ExpectedAfterSignIn, name);

            // The forum page takes the board of a topic opened first for the
            // board it holds, and offers the new topic from it on the way back.
            var topicId = @case.ModeratorsOnly ? seed.ModerationTopicId : seed.OpenTopicId;
            var topicBoard = (await ReadAsync(viewer, banned, $"/v1/topics/{topicId}"))
                .GetProperty("board").GetProperty("viewer");
            topicBoard.GetProperty("canCreateTopic").GetBoolean().Should().Be(flag);
            topicBoard.GetProperty("canCreateTopicAfterSignIn").GetBoolean().Should().Be(@case.ExpectedAfterSignIn);
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// The list of boards offers nothing to write, and the topic list of a
    /// board carries the board in every row: neither answers for the viewer.
    /// </summary>
    [Fact]
    public async Task LeaveTheBoardFlagOffTheLists()
    {
        var seed = await SeedAsync();
        try
        {
            var boards = (await ListAsync(seed.Stranger, "/v1/boards")).ToArray();
            boards.Should().Contain(b => b.GetProperty("id").GetGuid() == seed.OpenBoardId);
            boards.Should().AllSatisfy(b => b.TryGetProperty("viewer", out _).Should().BeFalse());

            var topics = (await ListAsync(seed.Stranger, $"/v1/boards/{seed.OpenBoardId}/topics")).ToArray();
            topics.Should().Contain(t => t.GetProperty("id").GetGuid() == seed.OpenTopicId);
            topics.Should().AllSatisfy(t =>
                t.GetProperty("board").TryGetProperty("viewer", out _).Should().BeFalse());
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    // ═══ THE REVIEW OF A GAME ═══

    /// <summary>Who reads the game and writes the review.</summary>
    public enum Reviewer
    {
        Guest,

        /// <summary>A member past the probation with a post in the game.</summary>
        Player,

        /// <summary>A member past the probation with no post in the game.</summary>
        Stranger,

        /// <summary>A member with a post in the game and under a hundred posts on the site.</summary>
        Newbie
    }

    /// <summary>What the reviewer left on the game before.</summary>
    public enum GameReviewBefore
    {
        Nothing,
        Review,
        RemovedReview
    }

    private sealed record GameCase(Reviewer Reviewer, GameReviewBefore Before, bool Expected);

    private static readonly Dictionary<string, GameCase> GameTable = new()
    {
        ["a player with a post"] = new(Reviewer.Player, GameReviewBefore.Nothing, true),
        ["a guest"] = new(Reviewer.Guest, GameReviewBefore.Nothing, false),
        ["a member with no post in the game"] = new(Reviewer.Stranger, GameReviewBefore.Nothing, false),
        ["a newbie with a post"] = new(Reviewer.Newbie, GameReviewBefore.Nothing, false),
        ["a player whose review is there"] = new(Reviewer.Player, GameReviewBefore.Review, false),
        ["a player whose review was removed"] = new(Reviewer.Player, GameReviewBefore.RemovedReview, true),
    };

    public static TheoryData<string> GameCases() => new(GameTable.Keys);

    [Theory]
    [MemberData(nameof(GameCases))]
    public async Task OfferTheGameReviewExactlyWhenTheReviewIsTaken(string name)
    {
        var @case = GameTable[name];
        var seed = await SeedAsync();
        try
        {
            var viewer = ReviewerOf(seed, @case.Reviewer);
            if (@case.Before != GameReviewBefore.Nothing)
            {
                await using var db = DatabaseFixture.CreateDbContext();
                db.Set<DbGameReview>().Add(new DbGameReview
                {
                    GameReviewId = Guid.NewGuid(),
                    AuthorId = viewer!.UserId,
                    GameId = seed.GameId,
                    CreatedUtc = DateTimeOffset.UtcNow.AddDays(-5),
                    Text = "Рецензия, написанная раньше",
                    IsRemoved = @case.Before == GameReviewBefore.RemovedReview
                });
                await db.SaveChangesAsync();
            }

            var flag = (await ReadAsync(viewer, false, $"/v1/games/{seed.GameId}/details"))
                .GetProperty("viewer").GetProperty("canReview").GetBoolean();
            var written = await Send(viewer, false, HttpMethod.Post, $"/v1/games/{seed.GameId}/reviews",
                new { text = "Рецензия, которую предлагает форма" });
            var body = await written.Content.ReadAsStringAsync();

            flag.Should().Be(@case.Expected, name);
            written.IsSuccessStatusCode.Should().Be(flag, $"the flag said {flag} and the review answered: {body}");
            if (!flag)
            {
                written.StatusCode.Should().Be(
                    viewer is null ? HttpStatusCode.Unauthorized
                    : @case.Before == GameReviewBefore.Review ? HttpStatusCode.Conflict
                    : HttpStatusCode.Forbidden, body);
            }
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    // ═══ THE RATING OF A POST ═══

    /// <summary>What the stranger rated before, and when.</summary>
    public enum RatedBefore
    {
        Nothing,

        /// <summary>The very post asked about, five days ago: out of the window, still rated.</summary>
        ThisPost,

        /// <summary>Another post of the same game, yesterday.</summary>
        GameYesterday,

        /// <summary>Another post of the same game, four days ago: the window has passed.</summary>
        GameFourDaysAgo,

        /// <summary>A post of another game, yesterday.</summary>
        OtherGameYesterday,

        /// <summary>Another post of the same game, yesterday, and the rating was removed.</summary>
        RemovedGameYesterday
    }

    private sealed record PostCase(Reviewer Reviewer, bool OwnPost, RatedBefore Before, bool Expected);

    private static readonly Dictionary<string, PostCase> PostTable = new()
    {
        ["a member on somebody else's post"] = new(Reviewer.Stranger, false, RatedBefore.Nothing, true),
        ["a guest"] = new(Reviewer.Guest, false, RatedBefore.Nothing, false),
        ["the author on his own post"] = new(Reviewer.Player, true, RatedBefore.Nothing, false),
        ["a member on a post he rated"] = new(Reviewer.Stranger, false, RatedBefore.ThisPost, false),
        ["a member who rated in the game yesterday"] = new(Reviewer.Stranger, false, RatedBefore.GameYesterday, false),
        ["a member who rated in the game four days ago"] =
            new(Reviewer.Stranger, false, RatedBefore.GameFourDaysAgo, true),
        ["a member who rated in another game yesterday"] =
            new(Reviewer.Stranger, false, RatedBefore.OtherGameYesterday, true),
        ["a member whose rating in the game yesterday was removed"] =
            new(Reviewer.Stranger, false, RatedBefore.RemovedGameYesterday, true),
        // The probation narrows the signs, not the form: "=" is offered.
        ["a newbie on somebody else's post"] = new(Reviewer.Newbie, false, RatedBefore.Nothing, true),
    };

    public static TheoryData<string> PostCases() => new(PostTable.Keys);

    [Theory]
    [MemberData(nameof(PostCases))]
    public async Task OfferThePostRatingExactlyWhenTheRatingIsTaken(string name)
    {
        var @case = PostTable[name];
        var seed = await SeedAsync();
        try
        {
            var viewer = ReviewerOf(seed, @case.Reviewer);
            var postId = @case.OwnPost ? seed.PlayerPostId : seed.MasterPostId;
            if (@case.Reviewer == Reviewer.Stranger && @case.Before != RatedBefore.Nothing)
            {
                // The post asked about is the master's. The rating before is of
                // that very post, or of the player's post of the same game, or
                // of the master's post in the other game.
                var (ratedPostId, ratedAuthorId, ratedGameId, daysAgo) = @case.Before switch
                {
                    RatedBefore.ThisPost => (seed.MasterPostId, seed.Master.UserId, seed.GameId, 5),
                    RatedBefore.GameFourDaysAgo => (seed.PlayerPostId, seed.Player.UserId, seed.GameId, 4),
                    RatedBefore.OtherGameYesterday => (seed.OtherGamePostId, seed.Master.UserId, seed.OtherGameId, 1),
                    _ => (seed.PlayerPostId, seed.Player.UserId, seed.GameId, 1)
                };
                await using var db = DatabaseFixture.CreateDbContext();
                db.Set<DbPostReview>().Add(new DbPostReview
                {
                    PostReviewId = Guid.NewGuid(),
                    PostId = ratedPostId,
                    PostAuthorId = ratedAuthorId,
                    AuthorId = viewer!.UserId,
                    GameId = ratedGameId,
                    SignValue = 0,
                    Text = "Оценка, оставленная раньше",
                    CreatedUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo).AddMinutes(1),
                    IsRemoved = @case.Before == RatedBefore.RemovedGameYesterday
                });
                await db.SaveChangesAsync();
            }

            var flag = CanReview(await ReadAsync(viewer, false, $"/v1/posts/{postId}"));
            // The room answers its posts with the same flag, one question for the page.
            var listed = (await ListAsync(viewer, $"/v1/rooms/{seed.RoomId}/posts"))
                .Single(p => p.GetProperty("id").GetGuid() == postId);
            CanReview(listed).Should().Be(flag, "the room and the post answer for the same viewer");

            var written = await Send(viewer, false, HttpMethod.Post, $"/v1/posts/{postId}/reviews",
                new { sign = "Neutral", text = "Оценка, которую предлагает форма" });
            var body = await written.Content.ReadAsStringAsync();

            flag.Should().Be(@case.Expected, name);
            written.IsSuccessStatusCode.Should().Be(flag, $"the flag said {flag} and the rating answered: {body}");
            if (!flag)
            {
                written.StatusCode.Should().Be(
                    viewer is null ? HttpStatusCode.Unauthorized
                    : @case.OwnPost ? HttpStatusCode.Forbidden
                    : HttpStatusCode.Conflict, body);
            }
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// A rating closes the game for three days: the page that offered the form
    /// under the other posts of the game learns it from the next read, and the
    /// write that raced it is told the rule in its own words, as a conflict and
    /// not as a rate limit.
    /// </summary>
    [Fact]
    public async Task CloseTheGameForThreeDaysWithARatingAndSayWhyToTheRace()
    {
        var seed = await SeedAsync();
        try
        {
            var rated = await Send(seed.Stranger, false, HttpMethod.Post, $"/v1/posts/{seed.MasterPostId}/reviews",
                new { sign = "Neutral", text = "Первая оценка в игре" });
            rated.IsSuccessStatusCode.Should().BeTrue(await rated.Content.ReadAsStringAsync());

            var room = (await ListAsync(seed.Stranger, $"/v1/rooms/{seed.RoomId}/posts")).ToArray();
            room.Should().HaveCount(3);
            room.Should().AllSatisfy(p => CanReview(p).Should().BeFalse());
            CanReview(await ReadAsync(seed.Stranger, false, $"/v1/posts/{seed.OtherGamePostId}"))
                .Should().BeTrue("the window is the game's, and the other game is not in it");

            var raced = await Send(seed.Stranger, false, HttpMethod.Post, $"/v1/posts/{seed.PlayerPostId}/reviews",
                new { sign = "Neutral", text = "Вторая оценка в той же игре" });
            var body = await raced.Content.ReadAsStringAsync();
            raced.StatusCode.Should().Be(HttpStatusCode.Conflict, body);
            using var problem = JsonDocument.Parse(body);
            problem.RootElement.GetProperty("title").GetString()
                .Should().Be("Оценивать посты в одной игре можно раз в три дня");
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    /// <summary>
    /// Every answer that carries posts carries the flag: the rated feed of the
    /// pulse and of the home page offers the rating under its posts as the room
    /// does, and a post it shows with no flag would lose the form there.
    /// </summary>
    [Fact]
    public async Task AnswerTheRatedFeedWithTheFlagOfEveryPost()
    {
        var seed = await SeedAsync();
        try
        {
            await using (var db = DatabaseFixture.CreateDbContext())
            {
                // The feed holds rated posts of the week.
                db.Set<DbPostReview>().Add(new DbPostReview
                {
                    PostReviewId = Guid.NewGuid(),
                    PostId = seed.PlayerPostId,
                    PostAuthorId = seed.Player.UserId,
                    AuthorId = seed.Master.UserId,
                    GameId = seed.GameId,
                    SignValue = 1,
                    Text = "Оценка для ленты",
                    CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1)
                });
                await db.SaveChangesAsync();
            }

            var feed = (await ListAsync(seed.Stranger, $"/v1/posts?gameId={seed.GameId}")).ToArray();
            feed.Should().Contain(p => p.GetProperty("id").GetGuid() == seed.PlayerPostId);
            CanReview(feed.Single(p => p.GetProperty("id").GetGuid() == seed.PlayerPostId)).Should().BeTrue();

            var asMaster = (await ListAsync(seed.Master, $"/v1/posts?gameId={seed.GameId}"))
                .Single(p => p.GetProperty("id").GetGuid() == seed.PlayerPostId);
            CanReview(asMaster).Should().BeFalse("the master rated it already");
        }
        finally
        {
            await DropAsync(seed);
        }
    }

    // ═══ SEEDING ═══

    /// <summary>
    /// The records of a case and the people around them: two boards, each with a
    /// topic; a game with an open room and posts of the master, a player and a
    /// newbie in it; another game with a post of the master.
    /// </summary>
    private sealed record Seed(
        GeneralUser Master,
        GeneralUser Player,
        GeneralUser Newbie,
        GeneralUser Stranger,
        GeneralUser Moderator)
    {
        public Guid OpenBoardId { get; } = Guid.NewGuid();
        public Guid ModerationBoardId { get; } = Guid.NewGuid();
        public Guid OpenTopicId { get; } = Guid.NewGuid();
        public Guid ModerationTopicId { get; } = Guid.NewGuid();
        public Guid GameId { get; } = Guid.NewGuid();
        public Guid RoomId { get; } = Guid.NewGuid();
        public Guid MasterPostId { get; } = Guid.NewGuid();
        public Guid PlayerPostId { get; } = Guid.NewGuid();
        public Guid NewbiePostId { get; } = Guid.NewGuid();
        public Guid OtherGameId { get; } = Guid.NewGuid();
        public Guid OtherRoomId { get; } = Guid.NewGuid();
        public Guid OtherGamePostId { get; } = Guid.NewGuid();
    }

    private async Task<Seed> SeedAsync()
    {
        var past = ProbationPolicy.NewbiePostThreshold * 2;
        var seed = new Seed(
            await SeedUserAsync("trf-mst", past),
            await SeedUserAsync("trf-ply", past),
            await SeedUserAsync("trf-nwb", 0),
            await SeedUserAsync("trf-str", past),
            await SeedUserAsync("trf-mod", past, UserRole.Moderator));
        var now = DateTimeOffset.UtcNow;

        await using var db = DatabaseFixture.CreateDbContext();

        AddBoard(db, seed.OpenBoardId, BoardAccessPolicy.RegularUser);
        // The news and the contests: topics come from the moderation.
        AddBoard(db, seed.ModerationBoardId, BoardAccessPolicy.Moderator);
        AddTopic(db, seed.OpenTopicId, seed.OpenBoardId, seed.Master.UserId);
        AddTopic(db, seed.ModerationTopicId, seed.ModerationBoardId, seed.Moderator.UserId);

        AddGame(db, seed.GameId, seed.Master.UserId);
        AddRoom(db, seed.RoomId, seed.GameId);
        AddPost(db, seed.MasterPostId, seed.RoomId, seed.Master.UserId, now.AddHours(-3));
        AddPost(db, seed.PlayerPostId, seed.RoomId, seed.Player.UserId, now.AddHours(-2));
        AddPost(db, seed.NewbiePostId, seed.RoomId, seed.Newbie.UserId, now.AddHours(-1));

        AddGame(db, seed.OtherGameId, seed.Master.UserId);
        AddRoom(db, seed.OtherRoomId, seed.OtherGameId);
        AddPost(db, seed.OtherGamePostId, seed.OtherRoomId, seed.Master.UserId, now.AddHours(-1));

        await db.SaveChangesAsync();
        return seed;
    }

    private static GeneralUser? ReviewerOf(Seed seed, Reviewer reviewer) => reviewer switch
    {
        Reviewer.Guest => null,
        Reviewer.Player => seed.Player,
        Reviewer.Newbie => seed.Newbie,
        _ => seed.Stranger
    };

    private async Task<GeneralUser> SeedUserAsync(string prefix, int posts, UserRole role = UserRole.RegularUser)
    {
        var userId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@topicreviewflag.example",
            PasswordHash = "hash",
            Salt = "salt",
            Role = role,
            // The probation reads the post counter, not a count of posts.
            QuantityRating = posts
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new GeneralUser { UserId = userId, Username = user.Username, Role = role };
    }

    private static void AddBoard(Infrastructure.Persistence.DmDbContext db, Guid boardId, BoardAccessPolicy createTopic) =>
        db.Set<DbBoard>().Add(new DbBoard
        {
            BoardId = boardId,
            Title = $"Topic flag board {boardId:N}",
            Alias = $"trf-{boardId:N}"[..20],
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = createTopic
        });

    private static void AddTopic(Infrastructure.Persistence.DmDbContext db, Guid topicId, Guid boardId, Guid authorId) =>
        db.Set<DbTopic>().Add(new DbTopic
        {
            TopicId = topicId,
            BoardId = boardId,
            AuthorId = authorId,
            Title = "Топик, через который открывают раздел",
            Text = "Текст топика",
            TopicNumber = 1,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1)
        });

    private static void AddGame(Infrastructure.Persistence.DmDbContext db, Guid gameId, Guid masterId)
    {
        db.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = gameId.ToString("N")[..10],
            Title = $"Review flag game {gameId:N}",
            MasterId = masterId,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
            ActivatedUtc = DateTimeOffset.UtcNow.AddDays(-9),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public
        });
        // The reads of a game fail loudly without its sheet.
        db.Add(GameSheetTestHelper.Sheet(gameId));
    }

    private static void AddRoom(Infrastructure.Persistence.DmDbContext db, Guid roomId, Guid gameId) =>
        db.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Комната",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1
        });

    private static void AddPost(
        Infrastructure.Persistence.DmDbContext db, Guid postId, Guid roomId, Guid authorId, DateTimeOffset createdUtc) =>
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = postId,
            RoomId = roomId,
            AuthorId = authorId,
            CreatedUtc = createdUtc,
            GameText = "Пост"
        });

    /// <summary>
    /// Everything the case made, and everything its writes made, but the
    /// people: their rows stay, the way the neighbouring suites leave theirs.
    /// </summary>
    private async Task DropAsync(Seed seed)
    {
        var games = new[] { seed.GameId, seed.OtherGameId };
        var rooms = new[] { seed.RoomId, seed.OtherRoomId };
        var boards = new[] { seed.OpenBoardId, seed.ModerationBoardId };
        await using var db = DatabaseFixture.CreateDbContext();

        await db.PostReviews.IgnoreQueryFilters().Where(r => games.Contains(r.GameId)).ExecuteDeleteAsync();
        await db.GameReviews.IgnoreQueryFilters().Where(r => games.Contains(r.GameId)).ExecuteDeleteAsync();
        await db.Set<DbPost>().IgnoreQueryFilters().Where(p => rooms.Contains(p.RoomId)).ExecuteDeleteAsync();
        await db.Set<DbRoom>().IgnoreQueryFilters().Where(r => games.Contains(r.GameId)).ExecuteDeleteAsync();
        // The sheet goes with the game (cascade).
        await db.Set<DbGame>().IgnoreQueryFilters().Where(g => games.Contains(g.GameId)).ExecuteDeleteAsync();

        var topics = await db.Set<DbTopic>().IgnoreQueryFilters().Where(t => boards.Contains(t.BoardId))
            .Select(t => t.TopicId).ToArrayAsync();
        await db.Set<DbBoard>().Where(b => boards.Contains(b.BoardId))
            .ExecuteUpdateAsync(b => b.SetProperty(x => x.LastTopicId, (Guid?)null));
        await db.UnreadCounters
            .Where(c => topics.Contains(c.EntityId) || boards.Contains(c.ParentId) || rooms.Contains(c.ParentId))
            .ExecuteDeleteAsync();
        await db.Set<DbTopic>().IgnoreQueryFilters().Where(t => boards.Contains(t.BoardId)).ExecuteDeleteAsync();
        await db.Set<DbBoard>().Where(b => boards.Contains(b.BoardId)).ExecuteDeleteAsync();
    }

    // ═══ REQUESTS ═══

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

    /// <summary>The resource of one record, as the viewer reads it.</summary>
    private async Task<JsonElement> ReadAsync(GeneralUser? user, bool banned, string url)
    {
        var response = await Send(user, banned, HttpMethod.Get, url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("resource").Clone();
    }

    /// <summary>The rows of a list, as the viewer reads it.</summary>
    private async Task<IEnumerable<JsonElement>> ListAsync(GeneralUser? user, string url)
    {
        var response = await Send(user, false, HttpMethod.Get, url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("resources").EnumerateArray()
            .Select(row => row.Clone()).ToArray();
    }

    private static bool CanReview(JsonElement post) =>
        post.GetProperty("viewer").GetProperty("canReview").GetBoolean();
}
