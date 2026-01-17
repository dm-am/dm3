# ContentMessage Component

A reusable content message component used for displaying user messages in chats and conversations.

## Location

`frontend/DM.Web.Modern.Temp/src/components/content/ContentMessage.vue`

## Description

ContentMessage is a content block for displaying user messages in chats and private messaging. It supports inline editing and provides all necessary functionality for message interaction.

## Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `message` | `MessageData` | required | Message data object |
| `isPublic` | `boolean` | `true` | Whether message is public (enables warning feature for moderators) |
| `maxHeight` | `number` | `300` | Maximum height before spoiler button appears |

### MessageData Interface

```typescript
interface MessageData {
  id: string;
  createdUtc: string;
  modifiedUtc: string | null;
  author: User;
  text: string;
  isRemoved: boolean;
  likes: User[];
}
```

## Events

| Event | Payload | Description |
|-------|---------|-------------|
| `edit` | `[id: string, text: string]` | Emitted when message is edited |
| `delete` | `[id: string]` | Emitted when message is deleted |
| `like` | `[id: string]` | Emitted when message is liked |
| `unlike` | `[id: string]` | Emitted when like is removed |
| `warn` | `[id: string]` | Emitted when warning button clicked (moderators only) |

## Features

### Visual Elements

1. **Avatar** - Small user avatar with link to profile
2. **Author name** - Username with link to profile
3. **Timestamp** - Time in HH:mm format with tooltip showing full date/time
4. **Edit status** - "(edited)" marker if message was modified
5. **Content** - Message text with BBCode rendering (v-html)
6. **Spoiler button** - Shows when content has [cut] tag or exceeds maxHeight
7. **Deleted placeholder** - Gray "Message deleted" block for soft-deleted messages
8. **Likes** - Heart icon with count and user list on hover
9. **Anchor link** - Chain icon to copy direct link to message

### Actions

1. **Edit** - Enters inline edit mode with textarea
   - Available to author within 15 minutes
   - Available to moderators without time limit
   - Ctrl+Enter to save, Escape to cancel

2. **Delete** - Soft deletes the message
   - Same permissions as Edit

3. **Like/Unlike** - Toggle like on message
   - Available to authenticated users
   - Cannot like own messages

4. **Warning** - Opens warning dialog (TODO)
   - Available to moderators only
   - Only for public messages

5. **Show/Hide deleted** - For moderators to view deleted message content

## Permission Logic

### Edit/Delete Permissions

```
IF user is moderator (Admin, SeniorModerator, Moderator):
  ALLOW always
ELSE IF user is author:
  ALLOW if less than 15 minutes since creation
ELSE:
  DENY
```

### Like Permissions

```
IF user is authenticated AND user is not author:
  ALLOW
ELSE:
  DENY
```

## Usage Example

```vue
<template>
  <content-message
    :message="message"
    :is-public="true"
    :max-height="400"
    @edit="handleEdit"
    @delete="handleDelete"
    @like="handleLike"
    @unlike="handleUnlike"
    @warn="handleWarn"
  />
</template>

<script setup lang="ts">
import ContentMessage from "@/components/content/ContentMessage.vue";

function handleEdit(id: string, text: string) {
  // Call API to update message
}

function handleDelete(id: string) {
  // Call API to delete message
}

function handleLike(id: string) {
  // Call API to like message
}

function handleUnlike(id: string) {
  // Call API to unlike message
}

function handleWarn(id: string) {
  // Open warning modal
}
</script>
```

## Backend Requirements

### Entity Fields

Both `ChatMessage` and `Message` entities must have:

- `CreateDate` - Creation timestamp
- `LastUpdateDate` - Last modification timestamp (nullable)
- `IsRemoved` - Soft delete flag
- `Likes` - Collection of likes

### API Endpoints (Chat)

- `PATCH /globalchat/messages/{id}` - Update message
- `DELETE /globalchat/messages/{id}` - Delete message
- `POST /globalchat/messages/{id}/likes` - Add like
- `DELETE /globalchat/messages/{id}/likes` - Remove like

### 15-Minute Edit Restriction

Implemented in `ChatIntentionResolver` and `MessageIntentionResolver`:
- Authors can edit/delete within 15 minutes
- Moderators can edit/delete without time limit

## Pages Using This Component

ContentMessage is used across the application through wrapper components:

### Global Chat
- **Page**: `src/views/pages/chat/ChatPage.vue`
- **Wrapper**: `ChatMessage.vue`
- **Props**: `isPublic: true`
- **Features**: Full message functionality with warning button for moderators

### Private Messaging
- **Page**: `src/views/pages/messenger/ConversationView.vue`
- **Wrapper**: `MessageItem.vue`
- **Props**: `isPublic: false`
- **Features**: Full message functionality, bubble layout for own messages, no warning button

### Forum Comments
- **Page**: `src/views/pages/topic/CommentsList.vue`
- **Wrapper**: `TheComment.vue`
- **Props**: `isPublic: true`
- **Features**: Full comment functionality with warning button for moderators

### Topic Opening Post
- **Page**: `src/views/pages/topic/TopicPage.vue`
- **Wrapper**: `TopicOpening.vue`
- **Props**: `isPublic: true`
- **Features**: Like/unlike, warning button (no edit/delete - topic editing handled separately)

## Related Components

| Component | Location | Description |
|-----------|----------|-------------|
| `ChatMessage.vue` | `src/views/pages/chat/` | Wrapper for global chat messages |
| `MessageItem.vue` | `src/views/pages/messenger/` | Wrapper for private messages with bubble layout |
| `TheComment.vue` | `src/components/comments/` | Wrapper for forum comments |
| `TopicOpening.vue` | `src/components/content/` | Wrapper for topic opening posts |

## Styling

Uses SASS with theme mixins. Key style variables:
- `$border` - Border color
- `$panel-background` - Background for deleted messages
- `$negative-text` - Color for likes and delete actions
- `$secondary-text` - Color for timestamps and secondary text
