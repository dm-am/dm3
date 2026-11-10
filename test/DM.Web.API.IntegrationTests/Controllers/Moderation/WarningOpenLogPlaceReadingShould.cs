using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

public class WarningOpenLogPlaceReadingShould : WarningOpenLogTestBase
{
    public WarningOpenLogPlaceReadingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ShowTheTextOfAHiddenGameToItsReadersAloneInTheOpenLog()
    {
        var warningId = LogId(0x03);
        var commentId = Guid.NewGuid();
        const string offending = "Мастер, ты вообще в своем уме";
        const string source = offending + " [private]СКРЫТЫЙ ТЕКСТ[/private]";
        var (hiddenGameId, masterId) = await SeedPrivateDraftGame();
        await SeedComment(commentId, hiddenGameId, source);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 1,
            entityId: commentId, entityType: WarningEntityType.Comment, snapshot: source));

        var url = $"/v1/warnings?username={TestConstants.SecondUserLogin}";
        var master = new GeneralUser { UserId = masterId, Username = "master", Role = UserRole.RegularUser };

        try
        {
            await AssertRestrictedToOutsiders(url, warningId);

            using (var document = await ReadDocument(
                       await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, master))))
            {
                var row = RowOf(document, warningId);
                row.GetProperty("evidence").GetString().Should().Contain(offending)
                    .And.NotContain("СКРЫТЫЙ ТЕКСТ", "a hidden block is shown to nobody outside a game post");
                row.GetProperty("placeKind").GetString().Should().Be("Game");
                row.GetProperty("placeTitle").GetString().Should().Be(PrivateDraftTitle);
                row.GetProperty("entityUrl").GetString().Should().EndWith($"#comment-{commentId}");
                row.TryGetProperty("accessRestricted", out _).Should().BeFalse();
            }

            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));
            var moderated = moderation.RootElement.GetProperty("resources").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == warningId);
            moderated.GetProperty("evidence").GetString().Should().Contain(offending)
                .And.NotContain("СКРЫТЫЙ ТЕКСТ", "the moderator reading the list is no exception");
            moderated.GetProperty("entityUrl").GetString().Should().EndWith($"#comment-{commentId}");
            moderated.GetProperty("placeKind").GetString().Should().Be("Game");
            moderated.GetProperty("placeTitle").GetString().Should().Be(PrivateDraftTitle);
            moderated.TryGetProperty("logClosure", out _).Should().BeFalse();
        }
        finally
        {
            await Unseed(warningId);
            await UnseedComment(commentId);
        }
    }

    /// <summary>
    /// A moderator reads a game awaiting the premoderation verdict, as the game's own read lets the rank in, and
    /// the open log gives him its text; a member outside the game gets the row of restricted access.
    /// </summary>
    [Fact]
    public async Task ShowTheTextOfAGameAwaitingTheVerdictToTheModeratorWhoReadsIt()
    {
        var warningId = LogId(0x08);
        var commentId = Guid.NewGuid();
        const string offending = "Реплика в игре на премодерации";
        var (gameId, _) = await SeedPrivateDraftGame(
            ModuleStatus.Active, PremoderationStatus.AwaitingApproval, DraftVisibility.Public);
        await SeedComment(commentId, gameId, offending);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 1,
            entityId: commentId, entityType: WarningEntityType.Comment, snapshot: offending));

        var url = $"/v1/warnings?username={TestConstants.SecondUserLogin}";

        try
        {
            using (var document = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                       HttpMethod.Get, url, CustomWebApplicationFactory.CreateModeratorUser()))))
            {
                var row = RowOf(document, warningId);
                row.GetProperty("evidence").GetString().Should().Contain(offending);
                row.GetProperty("placeTitle").GetString().Should().Be(PrivateDraftTitle);
                row.TryGetProperty("accessRestricted", out _).Should().BeFalse();
            }

            using var outsider = await ReadDocument(
                await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url)));
            AssertRestricted(RowOf(outsider, warningId));
        }
        finally
        {
            await Unseed(warningId);
            await UnseedComment(commentId);
        }
    }

    [Fact]
    public async Task ShowTheReviewOfAPostInAPrivateRoomToItsReadersAloneInTheOpenLog()
    {
        var warningId = LogId(0x09);
        const string offending = "Оценка поста в закрытой комнате";
        var (reviewId, player) = await SeedReviewInAPrivateRoom(offending);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 1,
            entityId: reviewId, entityType: WarningEntityType.PostReview, snapshot: offending));

        var url = $"/v1/warnings?username={TestConstants.SecondUserLogin}";

        try
        {
            await AssertRestrictedToOutsiders(url, warningId);

            using var document = await ReadDocument(
                await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, player)));
            var row = RowOf(document, warningId);
            row.GetProperty("evidence").GetString().Should().Contain(offending);
            row.GetProperty("placeKind").GetString().Should().Be("Game");
            row.GetProperty("placeTitle").GetString().Should().Be(RoomPostsTestHelper.GameTitle);
            row.GetProperty("entityUrl").GetString().Should().EndWith($"/post-reviews#review-{reviewId}");
            row.TryGetProperty("accessRestricted", out _).Should().BeFalse();
        }
        finally
        {
            await Unseed(warningId);
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShowTheTextOfAHiddenBlogToItsReadersAloneInTheOpenLog(bool underAPublication)
    {
        var warningId = LogId(underAPublication ? 0x0b : 0x0a);
        var commentId = Guid.NewGuid();
        const string offending = "Реплика в блоге в оформлении";
        var (blogId, publicId, publicationId, reader) = await SeedPrivateDraftBlog();
        await SeedComment(commentId, underAPublication ? publicationId : blogId, offending);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 1,
            entityId: commentId, entityType: WarningEntityType.Comment, snapshot: offending));

        var url = $"/v1/warnings?username={TestConstants.SecondUserLogin}";

        try
        {
            await AssertRestrictedToOutsiders(url, warningId);

            using var document = await ReadDocument(
                await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, reader)));
            var row = RowOf(document, warningId);
            row.GetProperty("evidence").GetString().Should().Contain(offending);
            row.GetProperty("placeKind").GetString().Should().Be(underAPublication ? "Publication" : "Blog");
            row.GetProperty("placeTitle").GetString().Should().Be(
                underAPublication ? HiddenPublicationTitle : HiddenBlogTitle);
            row.GetProperty("entityUrl").GetString().Should().Be(underAPublication
                ? $"/blogs/{publicId}/feed/{publicationId}#comment-{commentId}"
                : $"/blogs/{publicId}/comments#comment-{commentId}");
            row.TryGetProperty("accessRestricted", out _).Should().BeFalse();
        }
        finally
        {
            await Unseed(warningId);
            await UnseedComment(commentId);
        }
    }

    /// <summary>
    /// The row of a reader who does not read the place: one mark of restricted access, nothing of the text or of
    /// the place, the reason and the names as on every row.
    /// </summary>
    private static void AssertRestricted(JsonElement row)
    {
        row.GetProperty("accessRestricted").GetBoolean().Should().BeTrue();
        foreach (var field in new[] { "evidence", "placeKind", "placeTitle", "entityUrl", "entityCreatedUtc" })
        {
            row.TryGetProperty(field, out _).Should().BeFalse("{0} tells of a place the reader may not read", field);
        }

        row.GetProperty("reason").GetString().Should().Be("Причина предупреждения");
        row.GetProperty("user").GetProperty("username").GetString().Should().Be(TestConstants.SecondUserLogin);
    }

    private async Task AssertRestrictedToOutsiders(string url, Guid warningId)
    {
        HttpRequestMessage[] outsiders =
        [
            new(HttpMethod.Get, url),
            CreateAuthenticatedRequest(HttpMethod.Get, url),
            CreateAuthenticatedRequest(HttpMethod.Get, url, CustomWebApplicationFactory.CreateModeratorUser())
        ];

        foreach (var outsider in outsiders)
        {
            using var document = await ReadDocument(await Client.SendAsync(outsider));
            AssertRestricted(RowOf(document, warningId));
        }
    }

    private const string PrivateDraftTitle = "Игра в оформлении";

    private async Task<(Guid GameId, Guid MasterId)> SeedPrivateDraftGame(
        ModuleStatus status = ModuleStatus.Draft,
        PremoderationStatus premoderationStatus = PremoderationStatus.Approved,
        DraftVisibility draftVisibility = DraftVisibility.Private)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var master = NewUser("wpd");
        db.Users.Add(master);
        var game = new DbGame
        {
            GameId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = master.UserId,
            Title = PrivateDraftTitle,
            Status = status,
            PremoderationStatus = premoderationStatus,
            DraftVisibility = draftVisibility,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        db.Games.Add(game);
        db.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(game.GameId));
        await db.SaveChangesAsync();
        return (game.GameId, master.UserId);
    }

    private async Task<(Guid ReviewId, GeneralUser Player)> SeedReviewInAPrivateRoom(string text)
    {
        var room = await RoomPostsTestHelper.AddGameWithRoomAsync(DatabaseFixture, RoomAccessType.Private);
        var postId = await RoomPostsTestHelper.AddPostAsync(DatabaseFixture, room.RoomId, room.Player.UserId,
            room.PlayerCharacterId, DateTimeOffset.UtcNow.AddHours(-2));

        await using var db = DatabaseFixture.CreateDbContext();
        db.RoomAccesses.Add(new DbRoomAccess
        {
            AccessId = Guid.NewGuid(),
            RoomId = room.RoomId,
            CharacterId = room.PlayerCharacterId,
            Policy = RoomAccessPolicy.Full
        });
        var review = new DbPostReview
        {
            PostReviewId = Guid.NewGuid(),
            PostId = postId,
            AuthorId = TestConstants.SecondUserId,
            PostAuthorId = room.Player.UserId,
            GameId = room.GameId,
            Text = text,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1)
        };
        db.PostReviews.Add(review);
        await db.SaveChangesAsync();
        return (review.PostReviewId, room.Player);
    }

    private const string HiddenBlogTitle = "Блог в оформлении";

    private const string HiddenPublicationTitle = "Первая публикация";

    private async Task<(Guid BlogId, string PublicId, Guid PublicationId, GeneralUser Reader)> SeedPrivateDraftBlog()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var owner = NewUser("wbo");
        var reader = NewUser("wbr");
        db.Users.AddRange(owner, reader);
        var blog = new DbBlog
        {
            BlogId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = owner.UserId,
            Title = HiddenBlogTitle,
            Status = ModuleStatus.Draft,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Private,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        var publication = new DbPublication
        {
            PublicationId = Guid.NewGuid(),
            BlogId = blog.BlogId,
            PublicationNumber = 1,
            AuthorId = owner.UserId,
            Title = HiddenPublicationTitle,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        db.Blogs.Add(blog);
        db.Publications.Add(publication);
        db.Subscriptions.Add(new DbSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = reader.UserId,
            TargetType = SubscriptionTargetType.Blog,
            TargetId = blog.BlogId,
            Settings = SubscriptionSettings.InApp,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return (blog.BlogId, blog.PublicId, publication.PublicationId,
            new GeneralUser { UserId = reader.UserId, Username = reader.Username, Role = UserRole.RegularUser });
    }

    private static DbUser NewUser(string prefix)
    {
        var userId = Guid.NewGuid();
        return new DbUser
        {
            UserId = userId,
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@warning-log.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
    }
}
