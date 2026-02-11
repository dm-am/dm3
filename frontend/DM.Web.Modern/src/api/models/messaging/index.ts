import type { Id, Served } from "@/api/models";
import type { User } from "@/api/models/community";

export type ConversationId = Id<string>;
export type MessageId = Id<string>;

/**
 * Conversation type enum (matches backend ConversationType)
 */
export type ConversationType = "Direct" | "Group" | "Global";

export type Conversation = {
  id: Served<ConversationId>;
  type: Served<ConversationType>;
  participants: Served<User[]>;
  unreadMessagesCount: Served<number>;
  lastMessage?: Served<Message>;
  title?: Served<string | null>;
};

export type MessageEdit = {
  id: string;
  editedAtUtc: string;
  editor: User;
};

/**
 * Message model (unified for both private conversations and global chat).
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
  deletedAtUtc: string | null;
  likes: User[];
  edits: MessageEdit[];
};

/**
 * Input for creating a group conversation
 */
export type CreateConversation = {
  title: string;
  participantIds: string[];
};

/**
 * Input for updating a conversation (title and/or participants)
 */
export type UpdateConversation = {
  title?: string;
  addParticipants?: string[];
  removeParticipants?: string[];
};
