using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users.Settings;

namespace DM.Web.API.Notifications;

/// <summary>
/// Repository for accessing user notification settings from MongoDB
/// </summary>
public interface INotificationSettingsRepository
{
    /// <summary>
    /// Get user settings by user ID
    /// </summary>
    Task<UserSettings?> GetByUserId(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Insert or replace user settings
    /// </summary>
    Task Upsert(UserSettings settings, CancellationToken ct = default);
}
