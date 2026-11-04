using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Workers.NotificationDispatcher.Notifiers.Community;
using AwesomeAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A granted award is told to its recipient and leads to his hall of fame.
/// </summary>
/// <remarks>The address is built from the Username the generator writes, so the seed keeps recipient and granter apart.</remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class AwardGrantedNotificationsShould
{
    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0a0a-000000000001");
    private static readonly Guid GranterId = Guid.Parse("00000000-0000-0000-0a0a-000000000002");
    private static readonly Guid UserAwardId = Guid.Parse("00000000-0000-0000-0a0a-000000000011");

    /// <summary>"Литконкурс", first place: one of the award types InitialCreate ships.</summary>
    private static readonly Guid AwardTypeId = Guid.Parse("00000000-0000-0000-0001-000000000001");

    private const string RecipientUsername = "award-recipient";
    private const string GranterUsername = "award-granter";

    private readonly NotificationDatabaseFixture _fixture;

    public AwardGrantedNotificationsShould(NotificationDatabaseFixture fixture) => _fixture = fixture;

    private async Task SeedAsync()
    {
        await using var context = _fixture.CreateContext();
        if (context.UserAwards.Any(a => a.UserAwardId == UserAwardId))
        {
            return;
        }

        var created = DateTimeOffset.UtcNow.AddDays(-30);
        context.Users.AddRange(
            NotificationSeed.User(RecipientId, RecipientUsername, created),
            NotificationSeed.User(GranterId, GranterUsername, created));
        await context.SaveChangesAsync();

        context.UserAwards.Add(new UserAward
        {
            UserAwardId = UserAwardId,
            UserId = RecipientId,
            AwardTypeId = AwardTypeId,
            AwardedUtc = DateTimeOffset.UtcNow,
            AwardedByUserId = GranterId,
            IsRemoved = false
        });
        await context.SaveChangesAsync();
    }

    private async Task<CreateNotification> Granted()
    {
        await SeedAsync();
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new AwardGrantedNotificationGenerator(context), UserAwardId);
        return produced.Single();
    }

    [Fact]
    public async Task TellTheRecipient()
    {
        var notification = await Granted();

        notification.UsersInterested.Should().Equal(RecipientId);
    }

    [Fact]
    public async Task LeadToTheHallOfFameOfTheRecipient()
    {
        var notification = await Granted();

        NotificationDestination.GetPath(EventType.AwardGranted, notification.Metadata)
            .Should().Be($"/users/{RecipientUsername}?tab=achievements",
                "the award stands on the profile it was granted to, not on the granter's");
    }
}
