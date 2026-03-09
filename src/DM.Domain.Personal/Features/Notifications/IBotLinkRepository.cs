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
}
