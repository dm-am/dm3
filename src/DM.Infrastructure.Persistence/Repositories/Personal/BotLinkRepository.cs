using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public BotLinkRepository(
        DmDbContext dbContext,
        DmMongoClient mongoClient) : base(mongoClient)
    {
        _dbContext = dbContext;
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
        var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-10);

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
            var update = channelType.ToLowerInvariant() switch
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
}
