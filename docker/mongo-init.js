// MongoDB Initialization Script for DM3
// This script runs automatically on first container startup
// Documentation: docs/backend/OPTIMIZATION_OWN_GAMES.md
//
// This script is only the fast path for a fresh data volume: Mongo executes
// /docker-entrypoint-initdb.d exactly once, when the volume is empty, so nothing changed
// here can ever reach an already deployed database. The authority for the index set is
// MongoIndexInitializer (src/DM.Infrastructure.Persistence/MongoIntegration), which asserts
// the same indexes on every application startup and therefore also heals existing databases.
// Keep the two in sync; on any disagreement the application wins.

// Switch to the application database
db = db.getSiblingDB('dm3');

print('Creating indexes for DM3...');

// ============================================================================
// UnreadCounters Collection - Main performance-critical collection
// Used by: GameReadingService, ForumReadingService, ConversationReadingService
// ============================================================================

// Index for SelectByEntities - MOST CRITICAL for game sidebar performance
// Query: UserId IN [...], EntityId IN [...], EntryType = X, IsRemoved = false
db.UnreadCounters.createIndex(
    { UserId: 1, EntityId: 1, EntryType: 1, IsRemoved: 1 },
    { name: "IX_UnreadCounters_SelectByEntities", background: true }
);

// Index for SelectByParents, SelectTotalUnreadByParents
// Query: UserId IN [...], ParentId IN [...], EntryType = X, IsRemoved = false
db.UnreadCounters.createIndex(
    { UserId: 1, ParentId: 1, EntryType: 1, IsRemoved: 1 },
    { name: "IX_UnreadCounters_SelectByParents", background: true }
);

// Index for Increment, IncrementExcluding, Decrement, Delete, Flush
// Query: EntityId = X, EntryType = Y
db.UnreadCounters.createIndex(
    { EntityId: 1, EntryType: 1 },
    { name: "IX_UnreadCounters_Entity_Type", background: true }
);

// Index for FlushAll, ChangeParent
// Query: ParentId = X, EntryType = Y
db.UnreadCounters.createIndex(
    { ParentId: 1, EntryType: 1 },
    { name: "IX_UnreadCounters_Parent_Type", background: true }
);

print('UnreadCounters indexes created');

// ============================================================================
// UserSessions Collection - Authentication performance
// Used by: AuthenticationRepository
// ============================================================================

// Index for FindUserSession - runs on EVERY authenticated request
// Query: ElemMatch(Sessions, s => s.Id = X); same predicate in RefreshSession
// Session.Id is stored as the "_id" element of the embedded document, hence "Sessions._id"
// Every other query of this collection filters by the document _id (the user identifier)
db.UserSessions.createIndex(
    { "Sessions._id": 1 },
    { name: "IX_UserSessions_SessionId", background: true }
);

print('UserSessions indexes created');

// ============================================================================
// UserSettings Collection
// Used by: UserSettingsRepository, AuthenticationRepository
// Query: UserId = X (UserId is not the document _id)
// ============================================================================

db.UserSettings.createIndex(
    { UserId: 1 },
    { name: "IX_UserSettings_UserId", unique: true, background: true }
);

print('UserSettings indexes created');

// ============================================================================
// RealtimeNotifications Collection
// Used by: NotificationRepository
// ============================================================================

// Index for Count, CountUnread and GetNotifications
// Query: UsersInterested CONTAINS X, sorted by CreatedUtc descending
db.RealtimeNotifications.createIndex(
    { UsersInterested: 1, CreatedUtc: -1 },
    { name: "IX_RealtimeNotifications_Interested_Created", background: true }
);

// Index for MarkAsRead(notificationId, userId)
// Query: NotificationId = X (NotificationId is not the document _id)
db.RealtimeNotifications.createIndex(
    { NotificationId: 1 },
    { name: "IX_RealtimeNotifications_NotificationId", background: true }
);

print('RealtimeNotifications indexes created');

// ============================================================================
// Polls Collection
// Used by: PollRepository
// Get(id) filters by the document _id
// ============================================================================

// Index for Count and Get - every list query filters removed polls out
// Query: IsRemoved = false, range over StartsUtc, default sort by StartsUtc
db.Polls.createIndex(
    { IsRemoved: 1, StartsUtc: 1 },
    { name: "IX_Polls_Removed_Starts", background: true }
);

// Index for the same list query sorted by EndsUtc and for the "closed" status filter
// Query: IsRemoved = false, range over EndsUtc
db.Polls.createIndex(
    { IsRemoved: 1, EndsUtc: 1 },
    { name: "IX_Polls_Removed_Ends", background: true }
);

print('Polls indexes created');

// ============================================================================
// AttributeSchemata Collection (Game character attribute schemas)
// Used by: AttributeSchemaRepository
// Query: Type = Public OR UserId = X
// ============================================================================

db.AttributeSchemata.createIndex(
    { UserId: 1 },
    { name: "IX_AttributeSchemata_UserId", background: true }
);

print('AttributeSchemata indexes created');

// ============================================================================
// Dice Collection (Dice roll results)
// Used by: DiceRollRepository
// Query: PostId = X / PostId IN [...]
// ============================================================================

db.Dice.createIndex(
    { PostId: 1 },
    { name: "IX_Dice_PostId", background: true }
);

print('Dice indexes created');

// ============================================================================
// Summary
// ============================================================================

print('');
print('=== MongoDB Indexes Created Successfully ===');
print('Collections indexed:');
print('  - UnreadCounters (4 indexes) - CRITICAL for sidebar performance');
print('  - UserSessions (1 index) - CRITICAL for every authenticated request');
print('  - UserSettings (1 index)');
print('  - RealtimeNotifications (2 indexes)');
print('  - Polls (2 indexes)');
print('  - AttributeSchemata (1 index)');
print('  - Dice (1 index)');
print('');
print('Total: 12 indexes');
print('==========================================');
