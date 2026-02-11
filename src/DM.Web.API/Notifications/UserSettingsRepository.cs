using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users.Settings;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Driver;

namespace DM.Web.API.Notifications;

/// <summary>
/// Repository for accessing user settings from MongoDB for bot notifications
/// </summary>
internal class UserSettingsRepository : MongoCollectionRepository<UserSettings>, INotificationSettingsRepository
{
    /// <inheritdoc />
    public UserSettingsRepository(DmMongoClient client) : base(client)
    {
    }

    /// <summary>
    /// Get user settings by user ID
    /// </summary>
    public Task<UserSettings?> GetByUserId(Guid userId, CancellationToken ct = default)
    {
        return Collection
            .Find(Filter.Eq(u => u.UserId, userId))
            .FirstOrDefaultAsync(ct)!;
    }

    /// <summary>
    /// Insert or replace user settings
    /// </summary>
    public Task Upsert(UserSettings settings, CancellationToken ct = default)
    {
        return Collection.ReplaceOneAsync(
            Filter.Eq(u => u.UserId, settings.UserId),
            settings,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }
}
