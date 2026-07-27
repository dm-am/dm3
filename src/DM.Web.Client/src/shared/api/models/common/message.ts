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
  editor: User;
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
  author: Served<User>;
  text: string;
  isRemoved: boolean;
  deletedBy: User | null;
  deletedUtc: string | null;
  likes: User[];
  edits: MessageEdit[];
};
