using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
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
            ExpiresUtc = now.Add(CodeLifetime)
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

    /// <inheritdoc />
    public async Task<BotChannels> GetChannels(CancellationToken ct = default)
    {
        var userId = _identityProvider.Current.User.UserId;
        var ids = await _repository.GetChannelIds(userId, ct);
        var preferences = await _repository.GetChannelPreferences(userId, ct);

        return new BotChannels(
            Compose(ids.DiscordId, preferences.Discord),
            Compose(ids.TelegramId, preferences.Telegram));
    }

    /// <inheritdoc />
    public async Task UpdateChannelPreferences(
        string channelType,
        bool? enabled,
        IReadOnlyCollection<NotificationCategory>? categories,
        CancellationToken ct = default)
    {
        ValidateChannelType(channelType);
        var userId = _identityProvider.Current.User.UserId;

        var preferences = await _repository.GetChannelPreferences(userId, ct);
        var current = channelType.Equals("discord", StringComparison.OrdinalIgnoreCase)
            ? preferences.Discord
            : preferences.Telegram;

        // Preferences exist exactly while the channel is connected: linking writes
        // them together with the external id and disconnecting clears them together
        // with it. Their absence therefore means "there is nothing to configure", and
        // that is said out loud instead of being answered with a 200 that changed
        // nothing and a body showing the old values.
        if (current == null)
        {
            throw new HttpException(HttpStatusCode.Conflict,
                $"Канал {channelType} не подключен, настраивать нечего");
        }

        await _repository.SetChannelPreferences(
            userId,
            channelType,
            new ChannelPreferences(enabled ?? current.Enabled, categories ?? current.EnabledCategories),
            ct);
    }

    private static BotChannel? Compose(string? externalId, ChannelPreferences? preferences)
    {
        var connected = !string.IsNullOrEmpty(externalId);
        if (!connected && preferences == null)
        {
            return null;
        }

        return new BotChannel(
            connected,
            preferences?.Enabled ?? false,
            preferences?.EnabledCategories ?? Array.Empty<NotificationCategory>());
    }

    private static void ValidateChannelType(string channelType)
    {
        if (!Array.Exists(ValidChannelTypes, t => t.Equals(channelType, StringComparison.OrdinalIgnoreCase)))
            throw new HttpException(HttpStatusCode.BadRequest,
                $"Неизвестный канал: {channelType}. Доступны discord и telegram.");
    }
}
