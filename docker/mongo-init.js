// MongoDB Initialization Script for DM3
// This script runs automatically on first container startup
// Documentation: docs/backend/OPTIMIZATION_OWN_GAMES.md

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
// ============================================================================

db.UserSessions.createIndex(
    { UserId: 1, IsActive: 1 },
    { name: "IX_UserSessions_User_Active", background: true }
);

db.UserSessions.createIndex(
    { ExpirationDate: 1 },
    { name: "IX_UserSessions_Expiration", background: true }
);

print('UserSessions indexes created');

// ============================================================================
// UserSettings Collection
// ============================================================================

db.UserSettings.createIndex(
    { UserId: 1 },
    { name: "IX_UserSettings_UserId", unique: true, background: true }
);

print('UserSettings indexes created');

// ============================================================================
// RealtimeNotifications Collection
// ============================================================================

db.RealtimeNotifications.createIndex(
    { UserId: 1, IsRead: 1, CreateDate: -1 },
    { name: "IX_RealtimeNotifications_User_Read_Date", background: true }
);

db.RealtimeNotifications.createIndex(
    { UserId: 1, IsRemoved: 1 },
    { name: "IX_RealtimeNotifications_User_Removed", background: true }
);

print('RealtimeNotifications indexes created');

// ============================================================================
// Polls Collection
// ============================================================================

db.Polls.createIndex(
    { TopicId: 1 },
    { name: "IX_Polls_TopicId", background: true }
);

print('Polls indexes created');

// ============================================================================
// AttributeSchemata Collection (Game character attribute schemas)
// ============================================================================

db.AttributeSchemata.createIndex(
    { UserId: 1 },
    { name: "IX_AttributeSchemata_UserId", background: true }
);

print('AttributeSchemata indexes created');

// ============================================================================
// Dice Collection (Dice roll results)
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
print('  - UserSessions (2 indexes)');
print('  - UserSettings (1 index)');
print('  - RealtimeNotifications (2 indexes)');
print('  - Polls (1 index)');
print('  - AttributeSchemata (1 index)');
print('  - Dice (1 index)');
print('');
print('Total: 12 indexes');
print('==========================================');
