import type { Id, Served } from "@/api/models";
import type { User } from "@/api/models/community";

export type ConversationId = Id<string>;
export type MessageId = Id<string>;

export type Conversation = {
  id: Served<ConversationId>;
  participants: Served<User[]>;
  unreadMessagesCount: Served<number>;
  lastMessage?: Served<Message>;
};

export type Message = {
  id: Served<MessageId>;
  createdUtc: Served<string>;
  modifiedUtc: Served<string | null>;
  author: Served<User>;
  text: string;
  isRemoved: Served<boolean>;
  likes: Served<User[]>;
};
