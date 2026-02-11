# Database Architecture

**PostgreSQL 16** (54 tables) + **MongoDB** (8 collections)

---

## Overview

| Storage | Purpose | Tables/Collections |
|---------|---------|-------------------|
| PostgreSQL | Relational data, ACID, transactions | 54 |
| MongoDB | Schemaless, high-frequency writes, polymorphic data | 8 |

**Key Patterns:**
- **Soft Delete** (`IsRemoved` flag) on all main entities
- **Audit Trail** (`CreatedUtc`, `ModifiedUtc`, `DeletedByUserId`, `DeletedAtUtc`)
- **Edit History** (separate `*Edits` tables for Comments, Topics, Posts, Messages, Characters)
- **Outbox Pattern** for event-driven integrations
- **Denormalization** for read performance (LastComment* fields)

---

## PostgreSQL Tables (54)

### Domain: Users (11 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Users** | User accounts | UserId, Login, Email, Role, PasswordHash, QuantityRating, QualityRating, IsRemoved |
| **PendingRegistrations** | Unactivated registrations | PendingRegistrationId, Email, TokenId, PasswordHash, CreatedUtc |
| **Tokens** | Auth/activation/invite tokens | TokenId, UserId, Type, EntityId, PayloadHash, CreatedUtc |
| **ProfileNotes** | User's personal profile notes (visible to user) | ProfileNoteId, UserId, Text, CreatedUtc |
| **ProfileModNotes** | Moderator notes about users (private) | ProfileModNoteId, UserId, AuthorId, Text, CreatedUtc |
| **UserBlacklists** | User-to-user blocking | UserBlacklistId, UserId, BlockedUserId, CreatedUtc |
| **UserContacts** | User contact information | UserContactId, UserId, ContactType, Value |
| **LoginChangeRequests** | Pending login change requests | LoginChangeRequestId, UserId, NewLogin, CreatedUtc |
| **LoginHistories** | History of login changes | LoginHistoryId, UserId, OldLogin, NewLogin, ChangedAtUtc |
| **PasswordHistories** | History of password changes | PasswordHistoryId, UserId, ChangedAtUtc |
| **UserLoginRecords** | Login activity records | UserLoginRecordId, UserId, LoginAtUtc, IpAddress |

### Domain: Blogs (6 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Blogs** | User blogs | BlogId, OwnerId, Title, Status, AccessPolicy, MentorId |
| **BlogParticipants** | Blog access control | BlogParticipantId, BlogId, UserId, Participation |
| **BlogBlacklists** | Blog-level blocking | BlogBlacklistId, BlogId, UserId |
| **Rubrics** | Blog categories | RubricId, BlogId, Title, OrderNumber |
| **RubricAccesses** | Per-rubric access for participants | RubricAccessId, RubricId, BlogParticipantId, Policy |
| **Publications** | Blog articles | PublicationId, BlogId, RubricId, AuthorId, Title, Text, Status |

### Domain: Forum (4 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Boards** | Forum sections | BoardId, Title, Order, ViewPolicy, CreateTopicPolicy, TopicsCount*, CommentsCount* |
| **BoardModerators** | Section moderators | BoardModeratorId, BoardId, UserId |
| **Topics** | Forum topics | TopicId, BoardId, UserId, Title, Text, IsAttached, IsClosed |
| **TopicEdits** | Topic edit history | TopicEditId, TopicId, EditorUserId, EditedAtUtc |

### Domain: Games (12 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Games** | RPG games | GameId, MasterId, AssistantId, MentorId, Title, Status, CommentariesAccessMode |
| **Characters** | Game characters | CharacterId, GameId, UserId, Name, Status, AccessPolicy, IsNpc |
| **CharacterEdits** | Character edit history | CharacterEditId, CharacterId, EditorUserId, EditedAtUtc |
| **CharacterAttributes** | Character stats | CharacterAttributeId, CharacterId, SpecificationId, Value |
| **Rooms** | Game locations | RoomId, GameId, Title, AccessType, Type, OrderNumber, DiceEnabled |
| **RoomAccesses** | Character room access | RoomAccessId, RoomId, CharacterId |
| **Posts** | Game posts | PostId, RoomId, CharacterId, UserId, Text, Commentary, MasterMessage |
| **PostEdits** | Post edit history | PostEditId, PostId, EditorUserId, EditedAtUtc |
| **PostPendencies** | "Waiting for post" tracking | PostPendencyId, RoomId, AwaitingUserId, PendingUserId |
| **GameTags** | Game-tag links | GameTagId, GameId, TagId |
| **Readers** | Game observers | ReaderId, GameId, UserId |
| **GameBlacklists** | Game-level bans | GameBlacklistId, GameId, UserId |

### Domain: Messaging (6 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Conversations** | Private/group chats | ConversationId, Visavi, Title, LastMessageId* |
| **UserConversationLinks** | Conversation participants | UserConversationLinkId, UserId, ConversationId |
| **Messages** | Chat messages | MessageId, ConversationId, UserId, ChatEventId, Text |
| **MessageEdits** | Message edit history | MessageEditId, MessageId, EditorUserId, EditedAtUtc |
| **GlobalChatEvents** | Scheduled chat events | ChatEventId, Title, StartsAtUtc, Duration, Status, IsOpen |
| **GlobalChatEventParticipants** | Event participants | ChatEventParticipantId, ChatEventId, UserId, IsOrganizer |

### Domain: Common (8 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Comments** | Comments (polymorphic: Topics, Games) | CommentId, EntityId, UserId, Text |
| **CommentEdits** | Comment edit history | CommentEditId, CommentId, EditorUserId, EditedAtUtc |
| **Likes** | Likes (polymorphic: Comments, Topics, Messages) | LikeId, EntityId, UserId, CreatedUtc |
| **Reviews** | Site reviews | ReviewId, UserId, Text, CreatedUtc |
| **Tags** | Game tags | TagId, TagGroupId, Title |
| **TagGroups** | Tag categories | TagGroupId, Title |
| **Uploads** | File uploads | UploadId, UserId, GameId?, CharacterId?, PostId?, FilePath |
| **OutboxEvents** | Event queue (Outbox Pattern) | EventId, EntityId, EventType, CreatedUtc, ProcessedUtc |

### Domain: Notepads (2 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **NotepadEntries** | Notepad entries | EntryId, NotepadType, ContainerId, OwnerId, AuthorId, CategoryId, Title, Content |
| **NotepadCategories** | Entry categories | CategoryId, NotepadType, ContainerId, OwnerId, AuthorId, Name |

**NotepadType enum:** User(0), Master(1), Player(2), Blog(3)

### Domain: Administration (4 tables)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Tickets** | User reports/complaints | TicketId, AuthorId, TargetId, Reason, Status |
| **TicketResponses** | Ticket conversation history | TicketResponseId, TicketId, AuthorId, Text, IsFromModerator |
| **Warnings** | User warnings | WarningId, UserId, ModeratorId, Text, CommentId?, MessageId? |
| **Bans** | User bans | BanId, UserId, ModeratorId, Reason, StartUtc, EndUtc |

### Domain: Notifications (1 table)

| Table | Description | Key Columns |
|-------|-------------|-------------|
| **Subscriptions** | User subscriptions to entities | SubscriptionId, UserId, EntityType, EntityId, NotifyOnNew, NotifyOnReply |

---

## MongoDB Collections (8)

### Why MongoDB?

| Data Type | PostgreSQL | MongoDB |
|-----------|------------|---------|
| Fixed schema, relations | Good | Bad |
| ACID transactions | Good | Limited |
| Polymorphic structures | Bad | Good |
| High-frequency writes | Moderate | Good |
| Session arrays per user | Bad | Good |
| JSON with variable keys | Bad | Good |

### Collections

| Collection | Document Schema | Justification |
|------------|-----------------|---------------|
| **UserSettings** | `{ UserId, Paging, ColorSchema, BlacklistSettings }` | Schemaless preferences, nested objects |
| **UserSessions** | `{ UserId, Sessions: [{ SessionId, Token, CreatedAt, ... }] }` | Atomic array ops, single-document sessions per user |
| **RealtimeNotifications** | `{ UserId, Events: [{ Type, Metadata, CreatedAt }] }` | High throughput, variable metadata schema |
| **Dice** | `{ RollId, PostId, CharacterId, Results: [...], CreatedAt }` | Immutable logs, variable array sizes |
| **AttributeSchemata** | `{ SchemaId, Title, Specifications: [{ Type, Constraints }] }` | Polymorphic constraints (Number/String/List) |
| **UnreadCounters** | `{ UserId, EntityType, EntityId, Count, LastReadAt }` | Extreme write frequency, aggregation pipelines |
| **Polls** | `{ PollId, TopicId, Question, Options: [{ Text, Votes: [] }] }` | Dynamic options, embedded vote arrays |
| **LoginAttempts** | `{ Id (login), FailedAttempts, LastAttemptUtc, LockoutStartUtc? }` | Cluster-safe login throttling and lockout |

### Access Patterns

```csharp
// UserSettings - MongoCollectionRepository<UserSettings>
await Collection.FindOneAndUpdateAsync(
    u => u.UserId == userId,
    Builders<UserSettings>.Update.Set(u => u.ColorSchema, newSchema));

// UserSessions - atomic session operations
await Collection.UpdateOneAsync(
    s => s.UserId == userId,
    Builders<UserSessions>.Update.Push(s => s.Sessions, newSession));

// UnreadCounters - aggregation
var unread = await Collection.Aggregate()
    .Match(c => c.UserId == userId)
    .Group(c => c.EntityType, g => new { Type = g.Key, Total = g.Sum(c => c.Count) })
    .ToListAsync();
```

---

## Soft Delete Pattern

### Simple (IRemovable)
```csharp
public interface IRemovable
{
    bool IsRemoved { get; set; }
}
```
Tables: Users, Tokens, Reviews, Uploads, Games, Rooms, Bans, Blogs, Publications, etc.

### Extended (ISoftDeletable)
```csharp
public interface ISoftDeletable : IRemovable
{
    Guid? DeletedByUserId { get; set; }
    DateTimeOffset? DeletedAtUtc { get; set; }
}
```
Tables: Comments, Topics, Characters, Posts, Messages

**Global Query Filter:** EF Core HasQueryFilter автоматически добавляет `WHERE IsRemoved = false` ко всем запросам через `IRemovable` entities.

---

## Audit Fields

### Standard Audit (IEditable)
```csharp
public interface IEditable
{
    DateTimeOffset? ModifiedUtc { get; set; }
    Guid? ModifiedByUserId { get; set; }
}
```

### Edit History Tables
| Main Table | History Table | Fields |
|------------|---------------|--------|
| Comments | CommentEdits | CommentEditId, CommentId, EditorUserId, EditedAtUtc |
| Topics | TopicEdits | TopicEditId, TopicId, EditorUserId, EditedAtUtc |
| Characters | CharacterEdits | CharacterEditId, CharacterId, EditorUserId, EditedAtUtc |
| Posts | PostEdits | PostEditId, PostId, EditorUserId, EditedAtUtc |
| Messages | MessageEdits | MessageEditId, MessageId, EditorUserId, EditedAtUtc |

---

## Performance Indexes

Key composite indexes (see migration for full list):

```sql
-- User lookup (login page, @mentions)
CREATE INDEX IX_Users_IsRemoved_Login ON Users (IsRemoved, Login);

-- Game filtering (game lists)
CREATE INDEX IX_Games_IsRemoved_Status ON Games (IsRemoved, Status);

-- Topics in board
CREATE INDEX IX_Topics_BoardId_IsRemoved_IsAttached ON Topics (BoardId, IsRemoved, IsAttached);

-- Posts in room
CREATE INDEX IX_Posts_RoomId_IsRemoved ON Posts (RoomId, IsRemoved);

-- Characters in game
CREATE INDEX IX_Characters_GameId_IsRemoved_Status ON Characters (GameId, IsRemoved, Status);

-- Comments on entity
CREATE INDEX IX_Comments_EntityId_IsRemoved ON Comments (EntityId, IsRemoved);
```

---

## Entity Relationships

### Polymorphic Relations (EntityId pattern)
- **Comments.EntityId** -> Topics.TopicId | Games.GameId
- **Likes.EntityId** -> Comments.CommentId | Topics.TopicId | Messages.MessageId
- **Tokens.EntityId** -> Games.GameId (for invites)
- **Subscriptions.EntityId** -> any subscribable entity

### Many-to-Many
- Users <-> Conversations (via UserConversationLinks)
- Users <-> Boards (via BoardModerators)
- Games <-> Tags (via GameTags)
- Characters <-> Rooms (via RoomAccesses)
- Users <-> Blogs (via BlogParticipants)
- BlogParticipants <-> Rubrics (via RubricAccesses)

### Self-Reference
- Rooms.PreviousRoomId -> Rooms.RoomId (doubly-linked list)
- Rooms.NextRoomId -> Rooms.RoomId

---

## Global Chat Architecture

```
Conversations
    ├── ConversationId = 00000000-0000-0000-0000-000000000001 (Global Chat)
    │   └── Messages (all global chat messages)
    │       └── ChatEventId? -> GlobalChatEvents (optional event context)
    │
    └── Other ConversationIds (private/group chats)
        └── Messages
```

**GlobalChatEvents** enable scheduled community events with participant management.

---

## Migration

Single migration file: `20260208210947_InitialCreate.cs`

```bash
# Apply migrations
dotnet ef database update -p src/DM.Services.DataAccess -s src/DM.Web.API

# Generate new migration
dotnet ef migrations add MigrationName -p src/DM.Services.DataAccess -s src/DM.Web.API
```

---

## Resilience

### PostgreSQL (EF Core)

```csharp
EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5))
```

Автоматический retry при transient ошибках (network glitch, connection pool exhaustion).

### MongoDB

```csharp
RetryWrites = true;
RetryReads = true;
ServerSelectionTimeout = 5s;
ConnectTimeout = 10s;
```

---

## Links

- [Architecture Overview](./OVERVIEW.md)
- [Authentication](./AUTHENTICATION.md)
- [Code Standards](../standards/CODE.md)
- [Glossary](../reference/GLOSSARY.md)

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
