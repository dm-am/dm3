using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.MongoIntegration;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using TokenEntity = DM.Infrastructure.Persistence.Entities.Account.Token;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class BotLinkRepository : MongoCollectionRepository<UserSettings>, IBotLinkRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BotLinkRepository(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        IDateTimeProvider dateTimeProvider) : base(mongoClient)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task CreateLinkToken(CreateToken tokenDto, CancellationToken ct = default)
    {
        var tokenEntity = new TokenEntity
        {
            TokenId = tokenDto.TokenId,
            UserId = tokenDto.UserId,
            EntityId = tokenDto.EntityId,
            CreatedUtc = tokenDto.CreatedUtc,
            Type = tokenDto.Type,
            CreatorId = tokenDto.CreatorId,
            IsRemoved = false
        };
        _dbContext.Tokens.Add(tokenEntity);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Token?> FindValidToken(string code, CancellationToken ct = default)
    {
        var codeUpper = code.ToUpperInvariant();
        var cutoffTime = _dateTimeProvider.Now.AddMinutes(-10);

        // Load pending BotLink tokens, then filter in memory by first 6 chars of GUID
        var candidates = await _dbContext.Tokens
            .Where(t => t.Type == TokenType.NotificationBotLink &&
                        !t.IsRemoved &&
                        t.CreatedUtc > cutoffTime)
            .Select(t => new Token
            {
                TokenId = t.TokenId,
                UserId = t.UserId,
                CreatedUtc = t.CreatedUtc,
                Type = t.Type,
                IsRemoved = t.IsRemoved
            })
            .ToListAsync(ct);

        return candidates.FirstOrDefault(t =>
            t.TokenId.ToString()[..6].ToUpperInvariant() == codeUpper);
    }

    /// <inheritdoc />
    public async Task MarkTokenUsed(Guid tokenId, CancellationToken ct = default)
    {
        await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true), ct);
    }

    /// <inheritdoc />
    public async Task RemoveExistingTokens(Guid userId, CancellationToken ct = default)
    {
        await _dbContext.Tokens
            .Where(t => t.UserId == userId && t.Type == TokenType.NotificationBotLink)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true), ct);
    }

    /// <inheritdoc />
    public async Task SetChannelId(Guid userId, string channelType, string? externalId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) return;

        switch (channelType.ToLowerInvariant())
        {
            case "discord":
                user.DiscordId = externalId;
                break;
            case "telegram":
                user.TelegramId = externalId;
                break;
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BotChannelIds> GetChannelIds(Guid userId, CancellationToken ct = default)
    {
        var ids = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .Select(u => new { u.DiscordId, u.TelegramId })
            .FirstOrDefaultAsync(ct);

        return new BotChannelIds(ids?.DiscordId, ids?.TelegramId);
    }

    /// <inheritdoc />
    public async Task<string?> GetUsername(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Users
            .Where(u => u.UserId == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task InitializeChannelPreferences(Guid userId, string channelType, CancellationToken ct = default)
    {
        var filter = Filter.Eq(u => u.UserId, userId);
        var existingSettings = await Collection
            .Find(filter)
            .FirstOrDefaultAsync(ct);

        var defaultPreferences = new NotificationChannelPreference
        {
            Enabled = true,
            EnabledCategories = new HashSet<NotificationCategory>
            {
                NotificationCategory.Messages,
                NotificationCategory.Games,
                NotificationCategory.Security
            }
        };

        if (existingSettings == null)
        {
            var newSettings = UserSettings.CreateDefault(userId);

            switch (channelType.ToLowerInvariant())
            {
                case "discord":
                    newSettings.DiscordPreferences = defaultPreferences;
                    break;
                case "telegram":
                    newSettings.TelegramPreferences = defaultPreferences;
                    break;
            }

            await Collection.InsertOneAsync(newSettings, cancellationToken: ct);
        }
        else
        {
            UpdateDefinition<UserSettings> update = channelType.ToLowerInvariant() switch
            {
                "discord" => Update.Set(s => s.DiscordPreferences, defaultPreferences),
                "telegram" => Update.Set(s => s.TelegramPreferences, defaultPreferences),
                _ => throw new ArgumentException($"Invalid channel type: {channelType}")
            };

            await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
    }

    /// <inheritdoc />
    public async Task ClearChannelPreferences(Guid userId, string channelType, CancellationToken ct = default)
    {
        var filter = Filter.Eq(u => u.UserId, userId);
        var update = channelType.ToLowerInvariant() switch
        {
            "discord" => Update.Set(s => s.DiscordPreferences, null),
            "telegram" => Update.Set(s => s.TelegramPreferences, null),
            _ => throw new ArgumentException($"Invalid channel type: {channelType}")
        };

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<BotChannelPreferences> GetChannelPreferences(
        Guid userId, CancellationToken ct = default)
    {
        var settings = await Collection
            .Find(Filter.Eq(u => u.UserId, userId))
            .FirstOrDefaultAsync(ct);

        return new BotChannelPreferences(
            ToDomain(settings?.DiscordPreferences),
            ToDomain(settings?.TelegramPreferences));
    }

    /// <inheritdoc />
    public async Task SetChannelPreferences(
        Guid userId,
        string channelType,
        ChannelPreferences preferences,
        CancellationToken ct = default)
    {
        var stored = new NotificationChannelPreference
        {
            Enabled = preferences.Enabled,
            EnabledCategories = new HashSet<NotificationCategory>(preferences.EnabledCategories)
        };

        var update = channelType.ToLowerInvariant() switch
        {
            "discord" => Update.Set(s => s.DiscordPreferences, stored),
            "telegram" => Update.Set(s => s.TelegramPreferences, stored),
            _ => throw new ArgumentException($"Invalid channel type: {channelType}")
        };

        // Upsert with the defaults: a reader who never opened the settings page has
        // no document, and an update that matches nothing writes nothing, so the
        // preference was silently dropped. The rest of the document has to be the
        // default rather than empty, because a document without paging answers 500
        // on the next read of any list.
        var defaults = UserSettings.CreateDefault(userId);
        update = Update.Combine(
            update,
            Update.SetOnInsert(s => s.Theme, defaults.Theme),
            Update.SetOnInsert(s => s.Paging, defaults.Paging));

        await Collection.UpdateOneAsync(
            Filter.Eq(u => u.UserId, userId),
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    private static ChannelPreferences? ToDomain(NotificationChannelPreference? preference) =>
        preference == null
            ? null
            : new ChannelPreferences(preference.Enabled, preference.EnabledCategories);
}
