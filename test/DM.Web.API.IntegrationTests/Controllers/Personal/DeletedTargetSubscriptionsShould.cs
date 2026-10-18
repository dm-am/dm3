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
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// A deleted blog, game or topic takes the subscriptions to it along.
/// </summary>
/// <remarks>
/// The deletion hid the row and left the subscriptions pointing at it. The
/// subscriptions page names a target by reading its row, found nothing, and
/// listed the subscription as "Блог n/a" with an unsubscribe button: a row that
/// could never deliver anything, left to the reader to clean up. Nothing brings
/// a deleted blog, game or topic back, so the subscriptions go in the same
/// transaction as the deletion.
///
/// Written against HTTP: the reader's list is where the defect was seen, and
/// the deletion is the one the page performs.
/// </remarks>
public class DeletedTargetSubscriptionsShould : IntegrationTestBase
{
    public DeletedTargetSubscriptionsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Reader => CustomWebApplicationFactory.CreateSecondUser();
    private static GeneralUser Admin => CustomWebApplicationFactory.CreateAdminUser();

    public static TheoryData<SubscriptionTargetType> Targets => new()
    {
        SubscriptionTargetType.Blog,
        SubscriptionTargetType.Game,
        SubscriptionTargetType.Topic,
    };

    [Theory]
    [MemberData(nameof(Targets))]
    public async Task DropTheSubscriptionsToADeletedTarget(SubscriptionTargetType type)
    {
        var deleted = await AddTargetAsync(type, 0);
        var kept = await AddTargetAsync(type, 1);
        await SubscribeAsync(type, deleted.Id);
        await SubscribeAsync(type, kept.Id);
        (await ListedAsync()).Should().Contain([deleted.Id, kept.Id]);

        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Delete, deleted.Address, Admin));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var listed = await ListedAsync();
        listed.Should().NotContain(deleted.Id,
            "a subscription to something deleted can deliver nothing and names nothing");
        listed.Should().Contain(kept.Id, "only the subscriptions to the deleted target go");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Set<DbSubscription>().CountAsync(s => s.TargetId == deleted.Id)).Should().Be(0,
            "every reader's subscription to it goes, not only this reader's view of it");
    }

    /// <summary>
    /// A live target of the type, and the address its deletion is asked at. The
    /// topic number comes from this class's own range: the board is shared, and a
    /// number is unique on it.
    /// </summary>
    private async Task<(Guid Id, string Address)> AddTargetAsync(SubscriptionTargetType type, int index)
    {
        var id = Guid.NewGuid();
        var title = $"Deleted target {id:N}";
        await using var db = DatabaseFixture.CreateDbContext();
        switch (type)
        {
            case SubscriptionTargetType.Blog:
                db.Set<DbBlog>().Add(new DbBlog
                {
                    BlogId = id,
                    AuthorId = TestConstants.TestUserId,
                    Title = title,
                    Description = string.Empty,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    PublicId = id.ToString("N")[..10],
                    Status = ModuleStatus.Active,
                    PremoderationStatus = PremoderationStatus.Approved,
                });
                await db.SaveChangesAsync();
                return (id, $"/v1/blogs/{id}");

            case SubscriptionTargetType.Game:
                db.Set<DbGame>().Add(new DbGame
                {
                    GameId = id,
                    PublicId = id.ToString("N")[..10],
                    Title = title,
                    MasterId = TestConstants.TestUserId,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    Status = ModuleStatus.Active,
                    PremoderationStatus = PremoderationStatus.Approved,
                });
                // The reads of a game fail loudly without its sheet.
                db.AddRange(GameSheetTestHelper.Sheets([id]));
                await db.SaveChangesAsync();
                return (id, $"/v1/games/{id}");

            default:
                db.Set<DbTopic>().Add(new DbTopic
                {
                    TopicId = id,
                    BoardId = TestConstants.TestBoardId,
                    AuthorId = TestConstants.TestUserId,
                    Title = title,
                    Text = "Body",
                    TopicNumber = 9601 + index,
                    CreatedUtc = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync();
                return (id, $"/v1/topics/{id}");
        }
    }

    private async Task SubscribeAsync(SubscriptionTargetType type, Guid targetId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<DbSubscription>().Add(new DbSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = Reader.UserId,
            TargetType = type,
            TargetId = targetId,
            Settings = SubscriptionSettings.InApp,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>The targets on the reader's subscriptions page.</summary>
    private async Task<Guid[]> ListedAsync()
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/subscriptions", Reader));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resources")
            .EnumerateArray()
            .Select(s => s.GetProperty("targetId").GetGuid())
            .ToArray();
    }
}
