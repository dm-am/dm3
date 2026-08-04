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
using DbPoll = DM.Infrastructure.Persistence.Entities.Community.Poll;
using DbSecurityAuditEntry = DM.Infrastructure.Persistence.Entities.Account.SecurityAuditEntry;
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
/// Options that are part of the stored index descriptor (unique, TTL, and so on) must be
/// repeated here exactly, otherwise the server answers with an IndexOptionsConflict. Changing
/// a retention constant below therefore does not reach a database that already holds the
/// index: the value lives in the stored descriptor, and moving it takes an explicit drop and
/// recreate, which is a deliberate operation and not a startup hook's business.
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
            // The key the repository writes by: every upsert addresses a marker as
            // (UserId, EntityId, EntryType), so one document per triple is the
            // invariant the writes assume. Without the index two upserts racing
            // each other — two tabs, a double click on "mark as read" — leave two
            // markers, and every read has to defend itself against a state that
            // should not exist.
            // A database that already holds duplicates fails this assertion, and
            // the failure is logged rather than thrown; the way out is a reset,
            // not a repair.
            Index<DbUnreadCounter>("IX_UnreadCounters_User_Entity_Type", keys => keys
                .Ascending(c => c.UserId)
                .Ascending(c => c.EntityId)
                .Ascending(c => c.EntryType), unique: true),

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

            // A tombstone stops a deleted entity from returning through "mark as
            // read", and that is over in minutes; it used to stay forever, one
            // document per user per deleted topic, room and conversation, with
            // nothing collecting it. A live marker has no RemovedUtc element at
            // all, and a TTL index passes over what is not a date.
            Index<DbUnreadCounter>("IX_UnreadCounters_Expiry", keys => keys
                .Ascending(c => c.RemovedUtc),
                expireAfter: TimeSpan.FromDays(TombstoneRetentionDays)),
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

        // UserSettings — BotLinkRepository, UserRepository, AuthenticationRepository.FindUserSettings
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

            // Nothing ever deletes a notification: the repository has no delete at
            // all, so every notification ever raised for anyone stayed forever, and
            // the unread count paid for the whole history of a user on every page.
            // The documents carry identifiers of interested users along with game
            // and character names, which is the same argument that gave the
            // security trail its expiry — hence the same period.
            Index<DbNotification>("IX_RealtimeNotifications_Expiry", keys => keys
                .Ascending(n => n.CreatedUtc),
                expireAfter: TimeSpan.FromDays(NotificationRetentionDays)),
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
            // GetSchemata: Eq(s => s.IsRemoved, false) & (Eq(s => s.Type, Public) | Eq(s => s.UserId, userId))
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
            // of nowhere. This is the whole of the decay: the sweep that used to be
            // declared beside it was never called from anywhere, and it skipped
            // exactly the rows that outlive everything else — a record carrying a
            // lockout start is never read again once its pair stops trying.
            Index<DbLoginAttempt>("IX_LoginAttempts_Expiry", keys => keys
                .Ascending(a => a.LastAttemptUtc),
                expireAfter: TimeSpan.FromHours(LoginAttemptRetentionHours)),
        }, cancellationToken);

        // SecurityAuditLog — SecurityAuditRepository
        await Assert(client.GetCollection<DbSecurityAuditEntry>(), new[]
        {
            // Every read is "this user's events, newest first". The compound index
            // serves the equality and hands the sort back already ordered, so the
            // page no longer costs a collection scan plus a blocking sort.
            Index<DbSecurityAuditEntry>("IX_SecurityAuditLog_User_Time", keys => keys
                .Ascending(e => e.UserId)
                .Descending(e => e.TimestampUtc)),

            // The log holds addresses and user agents — personal data with no
            // expiry was the actual finding, not the missing index. A security
            // trail is useful for as long as an incident can still be
            // investigated, and unbounded after that is a liability, not an asset.
            Index<DbSecurityAuditEntry>("IX_SecurityAuditLog_Expiry", keys => keys
                .Ascending(e => e.TimestampUtc),
                expireAfter: TimeSpan.FromDays(SecurityAuditRetentionDays)),
        }, cancellationToken);
    }

    /// <summary>
    /// Mirrors AuthenticationConfiguration.LoginAttemptExpirationHours. A TTL index
    /// is part of the stored index descriptor, so it cannot be bound to configuration
    /// without dropping and recreating the index on every value change.
    /// </summary>
    private const int LoginAttemptRetentionHours = 24;

    /// <summary>
    /// Retention of the security audit trail. Same constraint as the login-attempt
    /// TTL: the value lives in the stored index descriptor, not in configuration.
    /// </summary>
    private const int SecurityAuditRetentionDays = 180;

    /// <summary>
    /// Retention of the notification stream. Same constraint as the two TTLs
    /// above: the value lives in the stored index descriptor, not in configuration.
    /// </summary>
    private const int NotificationRetentionDays = 180;

    /// <summary>
    /// How long a tombstoned unread marker is kept. Its job — refusing to revive a
    /// deleted entity through "mark as read" — is over as soon as the request that
    /// deleted the entity is, so anything above zero is slack, not a requirement.
    /// </summary>
    private const int TombstoneRetentionDays = 7;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static CreateIndexModel<TEntity> Index<TEntity>(
        string name,
        Func<IndexKeysDefinitionBuilder<TEntity>, IndexKeysDefinition<TEntity>> keys,
        bool unique = false,
        TimeSpan? expireAfter = null) =>
        new(keys(Builders<TEntity>.IndexKeys),
            new CreateIndexOptions { Name = name, Unique = unique, ExpireAfter = expireAfter });

    private Task Assert<TEntity>(
        IMongoCollection<TEntity> collection,
        IEnumerable<CreateIndexModel<TEntity>> indexes,
        CancellationToken cancellationToken) =>
        Assert(collection.Indexes, collection.CollectionNamespace.CollectionName, indexes, _logger,
            cancellationToken);

    /// <summary>
    /// Asserts one index per command.
    /// </summary>
    /// <remarks>
    /// createIndexes takes a whole batch and the server runs it whole or not at all, so a
    /// single descriptor the database disagrees about — an index someone created by hand, a
    /// TTL whose stored value differs from the constant — used to cost every other index of
    /// that collection. The failure was swallowed into a log line nobody reads, which is the
    /// right call for one index and the wrong price for five: the queries the rest of them
    /// serve went to collection scans with nothing to show for it.
    ///
    /// Still never stops the host: the application is fully functional without an index, only
    /// slower, and refusing to start over one trades a slow site for no site.
    /// </remarks>
    internal static async Task Assert<TEntity>(
        IMongoIndexManager<TEntity> indexManager,
        string collectionName,
        IEnumerable<CreateIndexModel<TEntity>> indexes,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var index in indexes)
        {
            var indexName = index.Options?.Name;
            try
            {
                await indexManager.CreateOneAsync(index, cancellationToken: cancellationToken);
                logger.LogDebug("[Mongo Indexes] {Collection}.{Index} asserted",
                    collectionName, indexName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Mongo Indexes] Failed to assert {Index} on {Collection}",
                    indexName, collectionName);
            }
        }
    }
}
