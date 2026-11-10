using System;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Profiles;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

internal partial class UserRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// The settings write is load-modify-save with one retry on a lost insert
    /// race, and deliberately not a dynamic ON CONFLICT: the set of updated
    /// fields is decided by the caller field by field, and composing that
    /// statement dynamically costs more than repeating the existing retry
    /// semantics. The settings row has more than one writer (this method and
    /// the bot link), so the insert of a fresh row can lose; the loser reruns
    /// as an update of the row the winner created. Exempted by name in
    /// DurableWritesShould: the second write replays the first, it does not
    /// follow it.
    /// </remarks>
    public async Task UpdateUser(UpdateUserEntity userUpdate, UpdateUserSettingsEntity settingsUpdate)
    {
        var user = await _dbContext.Users.FindAsync(userUpdate.UserId);
        if (user == null) return;

        // Update user entity fields
        if (userUpdate.UpdateStatus) user.Status = userUpdate.Status;
        if (userUpdate.UpdateName) user.Name = userUpdate.Name;
        if (userUpdate.UpdateLocation) user.Location = userUpdate.Location;
        if (userUpdate.UpdateInfo) user.Info = userUpdate.Info;
        if (userUpdate.RatingDisabled?.Value is { } ratingDisabled) user.RatingDisabled = ratingDisabled;
        if (userUpdate.ShowBirthday?.Value is { } showBirthday) user.ShowBirthday = showBirthday;
        if (userUpdate.AvatarUploadId?.Value is { } avatarUploadId) user.AvatarUploadId = avatarUploadId;

        // The settings row travels in the same SaveChanges as the user fields:
        // one transaction, both or neither.
        var settings = await _dbContext.UserSettings.FindAsync(settingsUpdate.UserId);
        var inserting = settings == null;
        if (settings == null)
        {
            // A fresh row is the defaults with the caller's fields over them: a
            // partial row is unrepresentable (every paging column is NOT NULL),
            // so the rest has to be the default rather than empty.
            settings = UserSettings.CreateDefault(settingsUpdate.UserId);
            _dbContext.UserSettings.Add(settings);
        }

        ApplySettingsUpdate(settings, settingsUpdate);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (inserting && e.IsDuplicateKey())
        {
            // Another writer created the row between the read and the insert.
            // The whole SaveChanges rolled back, so the user fields are still
            // pending on the tracked entity; the settings insert becomes an
            // update of the winner's row.
            _dbContext.Entry(settings).State = EntityState.Detached;
            var existing = await _dbContext.UserSettings
                .TagWith("DM.User.Settings.RetryAfterLostInsert")
                .FirstAsync(s => s.UserId == settingsUpdate.UserId);
            ApplySettingsUpdate(existing, settingsUpdate);
            await _dbContext.SaveChangesAsync();
        }
    }

    private static void ApplySettingsUpdate(UserSettings settings, UpdateUserSettingsEntity settingsUpdate)
    {
        if (settingsUpdate.Theme?.Value is { } theme) settings.Theme = theme;
        // Blank is the same answer as absent - the reader asked for UTC - so it
        // is stored as absent and there is one spelling of "no zone" in the row.
        if (settingsUpdate.UpdateTimeZone)
            settings.TimeZoneId = string.IsNullOrWhiteSpace(settingsUpdate.TimeZoneId)
                ? null
                : settingsUpdate.TimeZoneId.Trim();
        if (settingsUpdate.PageSize?.Value is { } pageSize) settings.PageSize = pageSize;
    }

    private async Task<DM.Domain.Core.Identity.UserSettings> ReadSettings(Guid userId)
    {
        var userSettings = await _dbContext.UserSettings
            .TagWith("DM.User.Settings")
            .FirstOrDefaultAsync(s => s.UserId == userId);
        return userSettings == null
            ? DM.Domain.Core.Identity.UserSettings.Default
            : userSettings.ToUserSettings();
    }
}
