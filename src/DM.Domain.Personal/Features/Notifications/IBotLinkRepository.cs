using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Repository for bot link token operations
/// </summary>
public interface IBotLinkRepository
{
    /// <summary>
    /// Create a bot link token
    /// </summary>
    Task CreateLinkToken(CreateToken token, CancellationToken ct = default);

    /// <summary>
    /// Find a valid (non-expired, non-removed) bot link token by code
    /// </summary>
    Task<Token?> FindValidToken(string code, CancellationToken ct = default);

    /// <summary>
    /// Mark a token as used (IsRemoved = true)
    /// </summary>
    Task MarkTokenUsed(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Remove any existing pending bot link tokens for the user
    /// </summary>
    Task RemoveExistingTokens(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Set external channel ID on user (DiscordId or TelegramId)
    /// </summary>
    Task SetChannelId(Guid userId, string channelType, string? externalId, CancellationToken ct = default);

    /// <summary>
    /// External channel IDs of the user: Discord and Telegram, either of which
    /// may be null when the channel is not connected.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="SetChannelId"/>. Without it the API layer
    /// read these two columns straight from the DbContext while the repository
    /// owned every write to them — an asymmetry with nothing holding it in
    /// place.
    /// </remarks>
    Task<BotChannelIds> GetChannelIds(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get username by user ID
    /// </summary>
    Task<string?> GetUsername(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Ensure UserSettings document exists in MongoDB and initialize channel preferences
    /// </summary>
    Task InitializeChannelPreferences(Guid userId, string channelType, CancellationToken ct = default);

    /// <summary>
    /// Clear channel notification preferences in MongoDB
    /// </summary>
    Task ClearChannelPreferences(Guid userId, string channelType, CancellationToken ct = default);

    /// <summary>
    /// Notification preferences of both bot channels. A channel that was never
    /// connected has none.
    /// </summary>
    Task<BotChannelPreferences> GetChannelPreferences(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Replace the notification preferences of one channel.
    /// </summary>
    /// <remarks>
    /// Writes the one sub-document and nothing else. The settings document also
    /// carries the theme and the paging, which another repository writes field by
    /// field, so a whole-document write here loses whichever of them landed between
    /// the read and the write.
    /// </remarks>
    Task SetChannelPreferences(
        Guid userId,
        string channelType,
        ChannelPreferences preferences,
        CancellationToken ct = default);
}

/// <summary>
/// External bot channel identifiers of a user.
/// </summary>
/// <param name="DiscordId">Discord identifier, null when not connected.</param>
/// <param name="TelegramId">Telegram identifier, null when not connected.</param>
public record BotChannelIds(string? DiscordId, string? TelegramId);
