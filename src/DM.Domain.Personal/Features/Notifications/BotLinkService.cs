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

    private const int CodeLength = 6;
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

        await _repository.RemoveExistingTokens(userId, ct);

        // The first characters of the GUID become the human-readable code
        var tokenId = _guidFactory.Create();
        var token = new CreateToken
        {
            TokenId = tokenId,
            UserId = userId,
            Type = TokenType.NotificationBotLink,
            CreatedUtc = now
        };

        await _repository.CreateLinkToken(token, ct);

        var code = tokenId.ToString()[..CodeLength].ToUpperInvariant();
        return new BotLinkCode
        {
            Code = code,
            ExpiresUtc = now.Add(BotLinkCode.Lifetime)
        };
    }

    /// <inheritdoc />
    public async Task<BotLinkResult> VerifyAndLink(string code, string channelType, string externalId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeLength)
            return new BotLinkResult { Success = false, Error = "Неверный формат кода" };

        ValidateChannelType(channelType);

        var token = await _repository.FindValidToken(code, ct);
        if (token == null)
            return new BotLinkResult { Success = false, Error = "Код неверный или истек" };

        await _repository.MarkTokenUsed(token.TokenId, ct);
        await _repository.SetChannelId(token.UserId, channelType, externalId, ct);
        await _repository.InitializeChannelPreferences(token.UserId, channelType, ct);

        var username = await _repository.GetUsername(token.UserId, ct);
        return new BotLinkResult { Success = true, Username = username };
    }

    /// <inheritdoc />
    public async Task Disconnect(string channelType, CancellationToken ct = default)
    {
        ValidateChannelType(channelType);
        var userId = _identityProvider.Current.User.UserId;

        await _repository.SetChannelId(userId, channelType, null, ct);
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

        // Preferences exist exactly while the channel is connected, so their absence
        // is refused rather than answered with a 200 that changed nothing.
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
