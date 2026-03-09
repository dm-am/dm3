using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Service for linking/unlinking bot notification channels to user accounts
/// </summary>
public interface IBotLinkService
{
    /// <summary>
    /// Generate a verification code for linking a bot channel.
    /// Returns the code and its expiration time.
    /// </summary>
    Task<BotLinkCode> GenerateLinkCode(string channelType, CancellationToken ct = default);

    /// <summary>
    /// Verify a linking code from a bot and connect the external account.
    /// Called by the bot when user sends /connect CODE.
    /// </summary>
    Task<BotLinkResult> VerifyAndLink(string code, string channelType, string externalId, CancellationToken ct = default);

    /// <summary>
    /// Disconnect a bot channel from the current user's account
    /// </summary>
    Task Disconnect(string channelType, CancellationToken ct = default);
}
