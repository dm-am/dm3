using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Personal.Features.Notifications;

/// <inheritdoc />
internal class BotLinkService : IBotLinkService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBotLinkRepository _repository;

    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly string[] ValidChannelTypes = { "discord", "telegram" };

    public BotLinkService(
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IBotLinkRepository repository)
    {
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<BotLinkCode> GenerateLinkCode(string channelType, CancellationToken ct = default)
    {
        ValidateChannelType(channelType);
        var userId = _identityProvider.Current.User.UserId;
        var now = _dateTimeProvider.Now;

        // Remove any existing pending tokens for this user
        await _repository.RemoveExistingTokens(userId, ct);

        // Create new token - first 6 chars of GUID become the human-readable code
        var tokenId = _guidFactory.Create();
        var token = new CreateToken
        {
            TokenId = tokenId,
            UserId = userId,
            Type = TokenType.NotificationBotLink,
            CreatedUtc = now
        };

        await _repository.CreateLinkToken(token, ct);

        var code = tokenId.ToString()[..6].ToUpperInvariant();
        return new BotLinkCode
        {
            Code = code,
            ExpiresAt = now.Add(CodeLifetime)
        };
    }

    /// <inheritdoc />
    public async Task<BotLinkResult> VerifyAndLink(string code, string channelType, string externalId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return new BotLinkResult { Success = false, Error = "Invalid code format" };

        ValidateChannelType(channelType);

        var token = await _repository.FindValidToken(code, ct);
        if (token == null)
            return new BotLinkResult { Success = false, Error = "Invalid or expired code" };

        // Mark token as used
        await _repository.MarkTokenUsed(token.TokenId, ct);

        // Set external channel ID on user
        await _repository.SetChannelId(token.UserId, channelType, externalId, ct);

        // Initialize notification preferences with defaults
        await _repository.InitializeChannelPreferences(token.UserId, channelType, ct);

        var username = await _repository.GetUsername(token.UserId, ct);
        return new BotLinkResult { Success = true, Username = username };
    }

    /// <inheritdoc />
    public async Task Disconnect(string channelType, CancellationToken ct = default)
    {
        ValidateChannelType(channelType);
        var userId = _identityProvider.Current.User.UserId;

        // Clear external channel ID
        await _repository.SetChannelId(userId, channelType, null, ct);

        // Clear notification preferences for this channel
        await _repository.ClearChannelPreferences(userId, channelType, ct);
    }

    private static void ValidateChannelType(string channelType)
    {
        if (!Array.Exists(ValidChannelTypes, t => t.Equals(channelType, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Invalid channel type: {channelType}. Must be 'discord' or 'telegram'.");
    }
}
