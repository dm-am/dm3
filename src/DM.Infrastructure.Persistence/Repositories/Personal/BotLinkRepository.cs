using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal class BotLinkRepository : IBotLinkRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BotLinkRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task CreateLinkToken(CreateToken tokenDto, CancellationToken ct = default)
    {
        var tokenEntity = TokenRows.From(tokenDto);
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
    public Task InitializeChannelPreferences(Guid userId, string channelType, CancellationToken ct = default) =>
        UpsertChannelPreference(userId, channelType, new NotificationChannelPreference
        {
            Enabled = true,
            EnabledCategories = new HashSet<NotificationCategory>
            {
                NotificationCategory.Messages,
                NotificationCategory.Games,
                NotificationCategory.Security
            }
        }, ct);

    /// <inheritdoc />
    public async Task ClearChannelPreferences(Guid userId, string channelType, CancellationToken ct = default)
    {
        // The switch picks the column setter, the single statement below does
        // the one write: one UPDATE per call whichever channel it is.
        Action<UpdateSettersBuilder<UserSettings>> clear =
            channelType.ToLowerInvariant() switch
            {
                "discord" => s => s.SetProperty(
                    x => x.DiscordPreferences, (NotificationChannelPreference?)null),
                "telegram" => s => s.SetProperty(
                    x => x.TelegramPreferences, (NotificationChannelPreference?)null),
                _ => throw new ArgumentException($"Invalid channel type: {channelType}")
            };

        await _dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(clear, ct);
    }

    /// <inheritdoc />
    public async Task<BotChannelPreferences> GetChannelPreferences(
        Guid userId, CancellationToken ct = default)
    {
        var settings = await _dbContext.UserSettings
            .TagWith("DM.BotLink.ChannelPreferences")
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        return new BotChannelPreferences(
            ToDomain(settings?.DiscordPreferences),
            ToDomain(settings?.TelegramPreferences));
    }

    /// <inheritdoc />
    public Task SetChannelPreferences(
        Guid userId,
        string channelType,
        ChannelPreferences preferences,
        CancellationToken ct = default) =>
        UpsertChannelPreference(userId, channelType, new NotificationChannelPreference
        {
            Enabled = preferences.Enabled,
            EnabledCategories = new HashSet<NotificationCategory>(preferences.EnabledCategories)
        }, ct);

    /// <summary>
    /// Writes one channel's preference, creating the settings row if the user
    /// has none.
    /// </summary>
    /// <remarks>
    /// One atomic INSERT ... ON CONFLICT, and one method rather than two. A
    /// reader who never opened the settings page has no row, and an update that
    /// matches nothing writes nothing, so the preference was silently dropped.
    /// Reading first and inserting on null is the other half of the same
    /// defect: it is not atomic, the key is unique, and the loser of that race
    /// got a duplicate key error instead of the write it asked for. The rest of
    /// a fresh row is the defaults — only the insert branch writes them, the
    /// update branch touches nothing but this channel's column.
    /// </remarks>
    /// <param name="userId">Owner of the settings row.</param>
    /// <param name="channelType">Channel the preference belongs to.</param>
    /// <param name="preference">Preference to store.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task UpsertChannelPreference(
        Guid userId,
        string channelType,
        NotificationChannelPreference preference,
        CancellationToken ct)
    {
        var column = channelType.ToLowerInvariant() switch
        {
            "discord" => "DiscordPreferences",
            "telegram" => "TelegramPreferences",
            _ => throw new ArgumentException($"Invalid channel type: {channelType}")
        };

        var defaults = UserSettings.CreateDefault(userId);
        var json = JsonSerializer.Serialize(preference, JsonColumn.Options);

        // The column name comes from the two-armed switch above, never from the
        // caller's string; the values travel as parameters — which is why raw
        // SQL is sound here.
        var statement = $$"""
            INSERT INTO "UserSettings"
                ("UserId", "Theme", "TopicsPerPage", "CommentsPerPage", "PostsPerPage",
                 "MessagesPerPage", "EntitiesPerPage", "{{column}}")
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}::jsonb)
            ON CONFLICT ("UserId") DO UPDATE SET "{{column}}" = {7}::jsonb
            """;
        await _dbContext.Database.ExecuteSqlRawAsync(
            statement,
            [
                userId, (int)defaults.Theme, defaults.TopicsPerPage, defaults.CommentsPerPage,
                defaults.PostsPerPage, defaults.MessagesPerPage, defaults.EntitiesPerPage, json
            ],
            ct);
    }

    private static ChannelPreferences? ToDomain(NotificationChannelPreference? preference) =>
        preference == null
            ? null
            : new ChannelPreferences(preference.Enabled, preference.EnabledCategories);
}
