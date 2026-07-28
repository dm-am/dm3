using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbDiceRoll = DM.Infrastructure.Persistence.Entities.Game.Posts.DiceRoll;
using DbLoginAttempt = DM.Infrastructure.Persistence.Entities.Account.LoginAttempt;
using DbNotification = DM.Infrastructure.Persistence.Entities.Personal.Notifications.Notification;
using DbPoll = DM.Infrastructure.Persistence.Entities.Forum.Poll;
using DbSession = DM.Infrastructure.Persistence.Entities.Account.Session;
using DbUnreadCounter = DM.Infrastructure.Persistence.Entities.Shared.UnreadCounter;
using DbUserSession = DM.Infrastructure.Persistence.Entities.Account.UserSession;
using DbUserSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.UserSettings;

namespace DM.Infrastructure.Persistence.MongoIntegration;

/// <summary>
/// Declares and asserts the MongoDB index set on application startup.
///
/// Relation to docker/mongo-init.js: the init script is only a fast path for a fresh data
/// volume — Mongo executes /docker-entrypoint-initdb.d exactly once, on the first start of
/// an empty volume, so no correction made there can ever reach a database that already
/// exists. This class is the authority: it declares the same set and re-asserts it on every
/// start, which also heals already-deployed databases. When an index changes here, mirror it
/// in the script; if the two ever disagree, this file wins.
///
/// createIndexes is idempotent — an index whose name and key spec match an existing one is a
/// no-op on the server, so a restart costs nothing when everything is already in place.
/// Options that are part of the stored index descriptor (unique, and so on) must be repeated
/// here exactly, otherwise the server answers with an IndexOptionsConflict.
///
/// Obsolete indexes are not dropped: dropping is destructive and belongs to an explicit
/// operation, not to a startup hook.
/// </summary>
public class MongoIndexInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MongoIndexInitializer> _logger;

    /// <inheritdoc />
    public MongoIndexInitializer(
        IServiceProvider serviceProvider,
        ILogger<MongoIndexInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DmMongoClient>();

        // UnreadCounters — UnreadCountersRepository
        await Assert(client.GetCollection<DbUnreadCounter>(), new[]
        {
            // SelectByEntitiesAsync: UserId IN, EntityId IN, EntryType =, IsRemoved =
            Index<DbUnreadCounter>("IX_UnreadCounters_SelectByEntities", keys => keys
                .Ascending(c => c.UserId)
                .Ascending(c => c.EntityId)
                .Ascending(c => c.EntryType)
                .Ascending(c => c.IsRemoved)),

            // SelectByParentsAsync, SelectTotalUnreadByParentsAsync:
            // UserId IN, ParentId IN, EntryType =, IsRemoved =
            Index<DbUnreadCounter>("IX_UnreadCounters_SelectByParents", keys => keys
                .Ascending(c => c.UserId)
                .Ascending(c => c.ParentId)
                .Ascending(c => c.EntryType)
                .Ascending(c => c.IsRemoved)),

            // IncrementAsync, IncrementExcludingAsync, DecrementAsync, DeleteAsync, FlushAsync:
            // EntityId =, EntryType =
            Index<DbUnreadCounter>("IX_UnreadCounters_Entity_Type", keys => keys
                .Ascending(c => c.EntityId)
                .Ascending(c => c.EntryType)),

            // FlushAllAsync, ChangeParentAsync: ParentId =, EntryType =
            Index<DbUnreadCounter>("IX_UnreadCounters_Parent_Type", keys => keys
                .Ascending(c => c.ParentId)
                .Ascending(c => c.EntryType)),
        }, cancellationToken);

        // UserSessions — AuthenticationRepository
        // Every other query of this collection filters by UserSession.Id, which is the _id
        // of the document and is therefore already served by the default _id index.
        await Assert(client.GetCollection<DbUserSession>(), new[]
        {
            // FindUserSession — runs on every authenticated request:
            //   Find(ElemMatch(u => u.Sessions, s => s.Id == sessionId))
            // and the same predicate in RefreshSession. Without this index the lookup is a
            // collection scan over every user's session array.
            // The member path is resolved through the class map, so Session.Id becomes the
            // "_id" element of the embedded document and the key is "Sessions._id".
            Index<DbUserSession>("IX_UserSessions_SessionId", keys => keys
                .Ascending($"{nameof(DbUserSession.Sessions)}.{nameof(DbSession.Id)}")),
        }, cancellationToken);

        // UserSettings — UserSettingsRepository, AuthenticationRepository.FindUserSettings
        await Assert(client.GetCollection<DbUserSettings>(), new[]
        {
            // GetByUserId, Upsert, FindUserSettings: UserId =
            // UserId is not the _id of the document, and one user has exactly one settings
            // document — hence unique.
            Index<DbUserSettings>("IX_UserSettings_UserId", keys => keys
                .Ascending(s => s.UserId), unique: true),
        }, cancellationToken);

        // RealtimeNotifications — NotificationRepository
        await Assert(client.GetCollection<DbNotification>(), new[]
        {
            // Count, CountUnread: AnyEq(n => n.UsersInterested, userId)
            // GetNotifications: the same filter, sorted by Sort.Descending(n => n.CreatedUtc)
            // — the second key keeps the sort non-blocking.
            Index<DbNotification>("IX_RealtimeNotifications_Interested_Created", keys => keys
                .Ascending(n => n.UsersInterested)
                .Descending(n => n.CreatedUtc)),

            // MarkAsRead(notificationId, userId): Eq(n => n.NotificationId, notificationId)
            // NotificationId is not the _id of the document.
            Index<DbNotification>("IX_RealtimeNotifications_NotificationId", keys => keys
                .Ascending(n => n.NotificationId)),
        }, cancellationToken);

        // Polls — PollRepository
        // Get(id) filters by Poll.Id, which is the _id of the document.
        await Assert(client.GetCollection<DbPoll>(), new[]
        {
            // Count, Get: every list query starts with Eq(p => p.IsRemoved, false) and the
            // default sort is StartsUtc; status "pending"/"active" and the StartsFromUtc /
            // StartsToUtc filters range over the same field.
            Index<DbPoll>("IX_Polls_Removed_Starts", keys => keys
                .Ascending(p => p.IsRemoved)
                .Ascending(p => p.StartsUtc)),

            // The same list query with SortBy = "ends", plus status "closed"
            // (Lte(p => p.EndsUtc, now)) and the EndsFromUtc / EndsToUtc filters.
            Index<DbPoll>("IX_Polls_Removed_Ends", keys => keys
                .Ascending(p => p.IsRemoved)
                .Ascending(p => p.EndsUtc)),
        }, cancellationToken);

        // AttributeSchemata — AttributeSchemaRepository
        await Assert(client.GetCollection<DbAttributeSchema>(), new[]
        {
            // GetSchemata: Eq(s => s.Type, Public) | Eq(s => s.UserId, userId)
            Index<DbAttributeSchema>("IX_AttributeSchemata_UserId", keys => keys
                .Ascending(s => s.UserId)),
        }, cancellationToken);

        // Dice — DiceRollRepository
        await Assert(client.GetCollection<DbDiceRoll>(), new[]
        {
            // GetByPostIdAsync: Eq(d => d.PostId, postId)
            // GetByPostIdsAsync: In(d => d.PostId, postIds)
            Index<DbDiceRoll>("IX_Dice_PostId", keys => keys
                .Ascending(d => d.PostId)),
        }, cancellationToken);

        // LoginAttempts — LoginAttemptRepository
        await Assert(client.GetCollection<DbLoginAttempt>(), new[]
        {
            // ResetAttempts on a successful login clears the account's records from
            // every address: Eq(x => x.Email, email). The _id is the (email, address)
            // pair, so this predicate has no covering index of its own.
            Index<DbLoginAttempt>("IX_LoginAttempts_Email", keys => keys
                .Ascending(a => a.Email)),

            // Counters have to decay. Without this a handful of typos spread over
            // months accumulates to the lockout threshold and locks the account out
            // of nowhere. CleanupExpiredRecords exists but nothing calls it.
            Index<DbLoginAttempt>("IX_LoginAttempts_Expiry", keys => keys
                .Ascending(a => a.LastAttemptUtc),
                expireAfter: TimeSpan.FromHours(LoginAttemptRetentionHours)),
        }, cancellationToken);
    }

    /// <summary>
    /// Mirrors AuthenticationConfiguration.LoginAttemptExpirationHours. A TTL index
    /// is part of the stored index descriptor, so it cannot be bound to configuration
    /// without dropping and recreating the index on every value change.
    /// </summary>
    private const int LoginAttemptRetentionHours = 24;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static CreateIndexModel<TEntity> Index<TEntity>(
        string name,
        Func<IndexKeysDefinitionBuilder<TEntity>, IndexKeysDefinition<TEntity>> keys,
        bool unique = false,
        TimeSpan? expireAfter = null) =>
        new(keys(Builders<TEntity>.IndexKeys),
            new CreateIndexOptions { Name = name, Unique = unique, ExpireAfter = expireAfter });

    private async Task Assert<TEntity>(
        IMongoCollection<TEntity> collection,
        IEnumerable<CreateIndexModel<TEntity>> indexes,
        CancellationToken cancellationToken)
    {
        var collectionName = collection.CollectionNamespace.CollectionName;
        try
        {
            var created = await collection.Indexes.CreateManyAsync(indexes, cancellationToken);
            _logger.LogDebug("[Mongo Indexes] {Collection}: {Indexes} asserted",
                collectionName, string.Join(", ", created));
        }
        catch (Exception ex)
        {
            // Never stop the host over an index: the application is fully functional without
            // one, only slower. A conflict with an index created by hand is the usual cause.
            _logger.LogError(ex, "[Mongo Indexes] Failed to assert indexes on {Collection}", collectionName);
        }
    }
}
