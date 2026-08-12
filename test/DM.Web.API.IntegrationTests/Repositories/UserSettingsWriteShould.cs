using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Web.API.Notifications;
using DM.Domain.Personal.Features.Notifications;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The settings document has several independent writers — the profile update
/// sets the theme and paging, the bot link sets channels, the preferences screen
/// sets notification channels — so a writer may only touch its own fields.
/// </summary>
/// <remarks>
/// Preferences used to be saved by replacing the whole document. Two tabs were
/// enough to lose the theme, and any element the class does not declare (the
/// document ignores extra elements on read) was destroyed with no trace. Both
/// facts only exist against a live store, hence the container Mongo.
/// </remarks>
public class UserSettingsWriteShould : IntegrationTestBase
{
    public UserSettingsWriteShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task LeaveTheFieldsOfTheOtherWritersAlone()
    {
        var userId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var settings = UserSettings.CreateDefault(userId);
        settings.Theme = Theme.Dark;
        settings.DiscordPreferences = new NotificationChannelPreference { Enabled = false };
        await Collection(scope).InsertOneAsync(settings);

        await scope.ServiceProvider.GetRequiredService<IBotLinkRepository>()
            .SetChannelPreferences(
                userId,
                "discord",
                new ChannelPreferences(true, []));

        var stored = await Collection(scope).Find(Key(userId)).SingleAsync();
        stored.DiscordPreferences!.Enabled.Should().BeTrue("this is the field that was written");
        stored.Theme.Should().Be(Theme.Dark, "the theme belongs to the profile update");
        stored.Paging.TopicsPerPage.Should().Be(10, "so does paging");
    }

    /// <summary>
    /// The document declares [BsonIgnoreExtraElements], so a field the class does
    /// not know about is invisible to this process and very much present in the
    /// store. A whole-document replacement deletes it silently.
    /// </summary>
    [Fact]
    public async Task LeaveAnElementTheClassDoesNotDeclareAlone()
    {
        var userId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var document = new BsonDocument
        {
            { "UserId", new BsonBinaryData(userId, GuidRepresentation.Standard) },
            { "Theme", (int)Theme.Light },
            { "WrittenByAnotherVersion", "keep me" },
        };
        await RawCollection(scope).InsertOneAsync(document);

        await scope.ServiceProvider.GetRequiredService<IBotLinkRepository>()
            .SetChannelPreferences(
                userId,
                "discord",
                new ChannelPreferences(true, []));

        var stored = await RawCollection(scope)
            .Find(Builders<BsonDocument>.Filter.Eq("WrittenByAnotherVersion", "keep me"))
            .FirstOrDefaultAsync();
        stored.Should().NotBeNull("a writer that owns two fields must not delete the rest");
    }

    [Fact]
    public async Task CreateTheDocumentForAUserWhoHasNone()
    {
        var userId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IBotLinkRepository>()
            .SetChannelPreferences(
                userId,
                "discord",
                new ChannelPreferences(true, []));

        var stored = await Collection(scope).Find(Key(userId)).SingleAsync();
        stored.DiscordPreferences!.Enabled.Should().BeTrue();
        stored.Paging.Should().NotBeNull("a document without paging answers 500 on the next read");
    }

    /// <summary>
    /// The first link of a channel is the other write that used to create the
    /// document, and it created it by reading first and inserting on null.
    /// </summary>
    [Fact]
    public async Task CreateTheDocumentOnTheFirstLinkOfAChannel()
    {
        var userId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IBotLinkRepository>()
            .InitializeChannelPreferences(userId, "telegram");

        var stored = await Collection(scope).Find(Key(userId)).SingleAsync();
        stored.TelegramPreferences!.Enabled.Should().BeTrue();
        stored.Paging.Should().NotBeNull("a document without paging answers 500 on the next read");
        stored.Theme.Should().Be(Theme.Light, "the rest of a fresh document is the default one");
    }

    /// <summary>
    /// The two branches used to disagree about an unknown channel: creating the
    /// document swallowed it and wrote a document with no preference at all,
    /// updating one refused. One write, one answer.
    /// </summary>
    [Fact]
    public async Task RefuseAChannelItDoesNotKnow()
    {
        var userId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBotLinkRepository>();

        var link = () => repository.InitializeChannelPreferences(userId, "carrier pigeon");

        await link.Should().ThrowAsync<ArgumentException>();
        (await Collection(scope).Find(Key(userId)).AnyAsync())
            .Should().BeFalse("a refused write leaves no document behind");
    }

    private static IMongoCollection<UserSettings> Collection(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmMongoClient>().GetCollection<UserSettings>();

    private static IMongoCollection<BsonDocument> RawCollection(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DmMongoClient>()
            .GetCollection<UserSettings>().Database
            .GetCollection<BsonDocument>("UserSettings");

    private static FilterDefinition<UserSettings> Key(Guid userId) =>
        Builders<UserSettings>.Filter.Eq(s => s.UserId, userId);
}
