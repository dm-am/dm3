// In shared so that entities reference Message without importing each other.

import type { User } from "./user";
import type { Served } from "@/shared/api/models";

export type ChatId = Served<string>;
export type MessageId = Served<string>;

/** Matches the backend ChatType. */
export type ChatType = "Direct" | "Group" | "Global";

type MessageEdit = {
  id: string;
  modifiedUtc: string;
  /** Null when the editor's account was removed. */
  editor: User | null;
};

/**
 * The message of private chats and the global chat. isRemoved, deletedBy and
 * likes are set locally by stores, so they are not Served.
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
 * An unread total: over every conversation (GET v1/chats/unread) or over the
 * notifications (GET v1/users/me/notifications/unread). The name pairs it with
 * the DTO UnreadCount in contract.spec.ts.
 */
export type UnreadCount = {
  count: number;
};
