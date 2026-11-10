using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

// The settings row has several independent writers — the profile update sets the theme and paging,
// the bot link sets channels, the preferences screen sets notification channels — so a writer may
// only touch its own columns.
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
        stored.PageSize.Should().Be(PagingPolicy.DefaultPageSize, "so does the page size");
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
        stored.PageSize.Should().Be(PagingPolicy.DefaultPageSize,
            "the rest of a fresh row is the defaults: a partial row is unrepresentable");
    }

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
        stored.PageSize.Should().Be(PagingPolicy.DefaultPageSize);
    }

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

    // The page size column refuses NULL, so a partial row cannot be written.
    [Fact]
    public async Task RefuseAPartialRow()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var act = () => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserSettings" ("UserId", "Theme", "PageSize")
            VALUES ({userId}, 0, NULL)
            """);

        await act.Should().ThrowAsync<Exception>(
            "a row without a page size is a partial document, and the schema refuses it");
    }

    // Issued one after the other: both calls share one scoped context.
    [Fact]
    public async Task CreateTheRowWholeOnTheSecondChannelWrite()
    {
        var userId = await SeedUser();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBotLinkRepository>();

        await repository.SetChannelPreferences(userId, "discord", new ChannelPreferences(true, []));
        await repository.SetChannelPreferences(userId, "telegram", new ChannelPreferences(false, []));

        var stored = await Read(scope, userId);
        stored.DiscordPreferences!.Enabled.Should().BeTrue();
        stored.TelegramPreferences!.Enabled.Should().BeFalse();
        stored.PageSize.Should().Be(PagingPolicy.DefaultPageSize, "whoever created the row wrote it whole");
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
