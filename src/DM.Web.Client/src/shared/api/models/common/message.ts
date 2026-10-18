/**
 * Base message types for shared usage across entities
 * @module shared/api/models/common/message
 *
 * These types are extracted from entities/message to allow other entities
 * (like global-chat) to reference Message without creating entities→entities imports.
 *
 * FSD Rule: entities should import from shared, not from each other.
 */

import type { User } from "./user";
import type { Id, Served } from "@/shared/api/models";

// ============================================================================
// Message Types
// ============================================================================

export type ChatId = Id<string>;
export type MessageId = Id<string>;

/**
 * Chat type enum (matches backend ChatType)
 */
export type ChatType = "Direct" | "Group" | "Global";

/**
 * Message edit history entry
 */
export type MessageEdit = {
  id: string;
  modifiedUtc: string;
  /** Null when the editor's account was removed. */
  editor: User | null;
};

/**
 * Base message DTO used across private chats and global chat.
 * Note: Fields like isRemoved, deletedBy, likes may be modified locally by stores,
 * so they don't use Served<T> wrapper.
 */
export type Message = {
  id: Served<MessageId>;
  createdUtc: Served<string>;
  modifiedUtc: Served<string | null>;
  /** Null when the author's account was removed: the message stays, unsigned. */
  author: Served<User | null>;
  text: string;
  isRemoved: boolean;
  deletedBy: User | null;
  deletedUtc: string | null;
  likes: User[];
  edits: MessageEdit[];
};

/**
 * The reader's own total of unread messages, over every conversation they are
 * in. GET v1/chats/unread answers it inside an Envelope.
 *
 * The name is what pairs the mirror with the published schema
 * DM.Web.API.Features.Messaging.Chats.ChatUnreadCount in contract.spec.ts, so
 * it follows the DTO and not whatever the screen showing it is called.
 */
export type ChatUnreadCount = {
  count: number;
};
