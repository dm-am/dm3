using System.Net;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// The subscriptions page names a game and a blog in the colour of its status,
/// as the site names them everywhere, so a subscription row carries the status
/// of its game or blog and the day it opened.
/// </summary>
/// <remarks>
/// The row used to carry the name alone, and the page drew a closed game and a
/// game opened yesterday as the same ordinary link (UI_STANDARDS, "Цвет ссылок
/// игр и блогов по статусу"). Asked through HTTP: the reader's list is where the
/// colour is read from.
/// </remarks>
public class SubscriptionTargetStatusShould : IntegrationTestBase
{
    public SubscriptionTargetStatusShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Reader => CustomWebApplicationFactory.CreateSecondUser();

    [Fact]
    public async Task NameTheStatusAndTheOpeningOfAGameAndABlog()
    {
        var gameId = Guid.NewGuid();
        var blogId = Guid.NewGuid();
        var gameOpened = DateTimeOffset.UtcNow.AddDays(-400);
        var blogOpened = DateTimeOffset.UtcNow.AddDays(-2);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Set<DbGame>().Add(new DbGame
            {
                GameId = gameId,
                PublicId = gameId.ToString("N")[..10],
                Title = $"Closed game {gameId:N}",
                MasterId = TestConstants.TestUserId,
                CreatedUtc = gameOpened.AddDays(-1),
                ActivatedUtc = gameOpened,
                ClosedUtc = gameOpened.AddDays(100),
                Status = ModuleStatus.Closed,
                ClosedReason = ClosedReason.Finished,
                PremoderationStatus = PremoderationStatus.Approved,
            });
            // The reads of a game fail loudly without its sheet.
            db.AddRange(GameSheetTestHelper.Sheets([gameId]));
            db.Set<DbBlog>().Add(new DbBlog
            {
                BlogId = blogId,
                AuthorId = TestConstants.TestUserId,
                Title = $"New blog {blogId:N}",
                Description = string.Empty,
                CreatedUtc = blogOpened.AddDays(-1),
                ActivatedUtc = blogOpened,
                PublicId = blogId.ToString("N")[..10],
                Status = ModuleStatus.Active,
                PremoderationStatus = PremoderationStatus.Approved,
            });
            db.Set<DbSubscription>().AddRange(
                Subscription(SubscriptionTargetType.Game, gameId),
                Subscription(SubscriptionTargetType.Blog, blogId),
                Subscription(SubscriptionTargetType.Topic, TestConstants.TestTopicId));
            await db.SaveChangesAsync();
        }

        try
        {
            var rows = await RowsAsync();

            var game = rows[gameId];
            game.GetProperty("targetStatus").GetString().Should().Be(nameof(ModuleStatus.Closed));
            game.GetProperty("targetActivatedUtc").GetDateTimeOffset().Should()
                .BeCloseTo(gameOpened, TimeSpan.FromSeconds(1));

            var blog = rows[blogId];
            blog.GetProperty("targetStatus").GetString().Should().Be(nameof(ModuleStatus.Active));
            blog.GetProperty("targetActivatedUtc").GetDateTimeOffset().Should()
                .BeCloseTo(blogOpened, TimeSpan.FromSeconds(1));

            // A topic has no status of this kind: the field says nothing rather
            // than something borrowed from another table.
            var topic = rows[TestConstants.TestTopicId];
            Absent(topic, "targetStatus").Should().BeTrue();
            Absent(topic, "targetActivatedUtc").Should().BeTrue();
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Set<DbSubscription>()
                .Where(s => s.SubscriberId == Reader.UserId &&
                            (s.TargetId == gameId || s.TargetId == blogId || s.TargetId == TestConstants.TestTopicId))
                .ExecuteDeleteAsync();
            await db.Set<DbBlog>().IgnoreQueryFilters().Where(b => b.BlogId == blogId).ExecuteDeleteAsync();
            // The sheet goes with the game (cascade).
            await db.Set<DbGame>().IgnoreQueryFilters().Where(g => g.GameId == gameId).ExecuteDeleteAsync();
        }
    }

    private static DbSubscription Subscription(SubscriptionTargetType type, Guid targetId) => new()
    {
        SubscriptionId = Guid.NewGuid(),
        SubscriberId = Reader.UserId,
        TargetType = type,
        TargetId = targetId,
        Settings = SubscriptionSettings.InApp,
        CreatedUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>The reader's subscription rows, by target.</summary>
    private async Task<Dictionary<Guid, JsonElement>> RowsAsync()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/subscriptions", Reader));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("resources")
            .EnumerateArray()
            .ToDictionary(s => s.GetProperty("targetId").GetGuid(), s => s.Clone());
    }

    /// <summary>A null is written by omitting the property, or as a literal null.</summary>
    private static bool Absent(JsonElement row, string property) =>
        !row.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;
}
