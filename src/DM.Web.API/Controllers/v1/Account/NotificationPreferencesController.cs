using System.Linq;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users.Settings;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notifications;
using DM.Web.API.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// API for managing notification delivery preferences
/// </summary>
[ApiController]
[Route("v1/account/notification-preferences")]
[ApiExplorerSettings(GroupName = "Account")]
public class NotificationPreferencesController : ControllerBase
{
    private readonly IIdentityProvider _identityProvider;
    private readonly DmDbContext _dbContext;
    private readonly INotificationSettingsRepository _settingsRepository;

    /// <inheritdoc />
    public NotificationPreferencesController(
        IIdentityProvider identityProvider,
        DmDbContext dbContext,
        INotificationSettingsRepository settingsRepository)
    {
        _identityProvider = identityProvider;
        _dbContext = dbContext;
        _settingsRepository = settingsRepository;
    }

    /// <summary>
    /// Get current notification preferences
    /// </summary>
    /// <response code="200">Notification preferences retrieved</response>
    /// <response code="401">Not authenticated</response>
    [HttpGet(Name = nameof(GetPreferences))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotificationPreferencesDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = _identityProvider.Current.User.UserId;

        var user = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .Select(u => new { u.DiscordId, u.TelegramId })
            .FirstOrDefaultAsync();

        var settings = await _settingsRepository.GetByUserId(userId);

        var dto = new NotificationPreferencesDto
        {
            Discord = MapChannelPreferences(user?.DiscordId, settings?.DiscordPreferences),
            Telegram = MapChannelPreferences(user?.TelegramId, settings?.TelegramPreferences)
        };

        return Ok(new Envelope<NotificationPreferencesDto>(dto));
    }

    /// <summary>
    /// Update notification preferences
    /// </summary>
    /// <param name="request">Preferences to update</param>
    /// <response code="200">Preferences updated successfully</response>
    /// <response code="401">Not authenticated</response>
    [HttpPatch(Name = nameof(UpdatePreferences))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<NotificationPreferencesDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        var userId = _identityProvider.Current.User.UserId;

        var settings = await _settingsRepository.GetByUserId(userId);
        if (settings == null)
        {
            settings = new UserSettings { UserId = userId };
        }

        if (request.Discord != null && settings.DiscordPreferences != null)
        {
            if (request.Discord.Enabled.HasValue)
                settings.DiscordPreferences.Enabled = request.Discord.Enabled.Value;
            if (request.Discord.EnabledCategories != null)
                settings.DiscordPreferences.EnabledCategories = request.Discord.EnabledCategories;
        }

        if (request.Telegram != null && settings.TelegramPreferences != null)
        {
            if (request.Telegram.Enabled.HasValue)
                settings.TelegramPreferences.Enabled = request.Telegram.Enabled.Value;
            if (request.Telegram.EnabledCategories != null)
                settings.TelegramPreferences.EnabledCategories = request.Telegram.EnabledCategories;
        }

        await _settingsRepository.Upsert(settings);

        // Return updated state
        var user = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .Select(u => new { u.DiscordId, u.TelegramId })
            .FirstOrDefaultAsync();

        var dto = new NotificationPreferencesDto
        {
            Discord = MapChannelPreferences(user?.DiscordId, settings.DiscordPreferences),
            Telegram = MapChannelPreferences(user?.TelegramId, settings.TelegramPreferences)
        };

        return Ok(new Envelope<NotificationPreferencesDto>(dto));
    }

    private static ChannelPreferencesDto? MapChannelPreferences(
        string? externalId,
        NotificationChannelPreferences? prefs)
    {
        var connected = !string.IsNullOrEmpty(externalId);
        if (!connected && prefs == null) return null;

        return new ChannelPreferencesDto
        {
            Connected = connected,
            Enabled = prefs?.Enabled ?? false,
            EnabledCategories = prefs?.EnabledCategories ?? new()
        };
    }
}
