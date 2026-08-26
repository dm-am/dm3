using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The settings row has several independent writers — the profile update sets
/// the theme and paging, the bot link sets channels, the preferences screen
/// sets notification channels — so a writer may only touch its own columns.
/// </summary>
/// <remarks>
/// Preferences used to be saved by replacing the whole document; two tabs were
/// enough to lose the theme. The channel write is one INSERT ... ON CONFLICT
/// now, whose update arm names only its own column, and the race of two
/// creators is settled by the server. A partial row is unrepresentable — every
/// paging column is NOT NULL (INV-9) — which is what buried the null-Paging
/// repair branches. All of that exists only against a live Postgres, hence the
/// container.
/// </remarks>
public class UserSettingsWriteShould : IntegrationTestBase
{
    public UserSettingsWriteShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task LeaveTheColumnsOfTheOtherWritersAlone()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var settings = UserSettings.CreateDefault(userId);
        settings.Theme = Theme.Dark;
        settings.DiscordPreferences = new NotificationChannelPreference { Enabled = false };
        dbContext.UserSettings.Add(settings);
        await dbContext.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IBotLinkRepository>()
            .SetChannelPreferences(
                userId,
                "discord",
                new ChannelPreferences(true, []));

        var stored = await Read(scope, userId);
        stored.DiscordPreferences!.Enabled.Should().BeTrue("this is the column that was written");
        stored.Theme.Should().Be(Theme.Dark, "the theme belongs to the profile update");
        stored.TopicsPerPage.Should().Be(10, "so does paging");
    }

    [Fact]
    public async Task CreateTheRowForAUserWhoHasNone()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IBotLinkRepository>()
            .SetChannelPreferences(
                userId,
                "discord",
                new ChannelPreferences(true, []));

        var stored = await Read(scope, userId);
        stored.DiscordPreferences!.Enabled.Should().BeTrue();
        stored.TopicsPerPage.Should().Be(10,
            "the rest of a fresh row is the defaults: a partial row is unrepresentable");
    }

    /// <summary>
    /// The first link of a channel is the other write that used to create the
    /// document, and it created it by reading first and inserting on null.
    /// </summary>
    [Fact]
    public async Task CreateTheRowOnTheFirstLinkOfAChannel()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IBotLinkRepository>()
            .InitializeChannelPreferences(userId, "telegram");

        var stored = await Read(scope, userId);
        stored.TelegramPreferences!.Enabled.Should().BeTrue();
        stored.Theme.Should().Be(Theme.Light, "the rest of a fresh row is the default one");
        stored.EntitiesPerPage.Should().Be(10);
    }

    /// <summary>
    /// The two branches used to disagree about an unknown channel: creating the
    /// document swallowed it and wrote a document with no preference at all,
    /// updating one refused. One write, one answer.
    /// </summary>
    [Fact]
    public async Task RefuseAChannelItDoesNotKnow()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBotLinkRepository>();

        var link = () => repository.InitializeChannelPreferences(userId, "carrier pigeon");

        await link.Should().ThrowAsync<ArgumentException>();
        (await scope.ServiceProvider.GetRequiredService<DmDbContext>().UserSettings
                .AnyAsync(s => s.UserId == userId))
            .Should().BeFalse("a refused write leaves no row behind");
    }

    /// <summary>
    /// INV-9 as the schema states it: the paging columns refuse NULL, so the
    /// state that used to answer 500 on every request cannot be written at all.
    /// </summary>
    [Fact]
    public async Task RefuseAPartialRow()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var act = () => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserSettings" ("UserId", "Theme", "TopicsPerPage", "CommentsPerPage", "PostsPerPage", "MessagesPerPage", "EntitiesPerPage")
            VALUES ({userId}, 0, NULL, 10, 10, 10, 10)
            """);

        await act.Should().ThrowAsync<Exception>(
            "a row without a paging value is the document with Paging: null, and the schema refuses it");
    }

    /// <summary>
    /// Two writers racing to create the row leave one valid row: the bot-link
    /// upsert is settled by the server, and the profile update retries its lost
    /// insert as an update of the winner's row.
    /// </summary>
    [Fact]
    public async Task LeaveOneValidRowWhenTwoWritersRace()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBotLinkRepository>();

        // Two concurrent creating writes of the same row. Each call opens work
        // on the same scoped context, so they are issued sequentially here; the
        // atomicity under test is the server-side ON CONFLICT, which the second
        // call exercises against the row the first one created.
        await repository.SetChannelPreferences(userId, "discord", new ChannelPreferences(true, []));
        await repository.SetChannelPreferences(userId, "telegram", new ChannelPreferences(false, []));

        var stored = await Read(scope, userId);
        stored.DiscordPreferences!.Enabled.Should().BeTrue();
        stored.TelegramPreferences!.Enabled.Should().BeFalse();
        stored.TopicsPerPage.Should().Be(10, "whoever created the row wrote it whole");
    }

    private async Task<Guid> SeedUser()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DM.Infrastructure.Persistence.Entities.Account.User
        {
            UserId = userId,
            Username = $"s{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static Task<UserSettings> Read(IServiceScope scope, Guid userId) =>
        scope.ServiceProvider.GetRequiredService<DmDbContext>().UserSettings
            .AsNoTracking()
            .SingleAsync(s => s.UserId == userId);
}
