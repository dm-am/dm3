import type { Served } from "@/shared/api/models";
import type {
  User,
  ChatId,
  ChatType,
  Message,
} from "@/shared/api/models/common";

export type { ChatId, MessageId, Message } from "@/shared/api/models/common";

export type Chat = {
  id: Served<ChatId>;
  type: Served<ChatType>;
  participants: Served<User[]>;
  unreadMessagesCount: Served<number>;
  lastMessage?: Served<Message>;
  title?: Served<string | null>;
  /** On the read of one chat and the writes that return one; the list leaves it out. */
  viewer?: Served<ChatViewer>;
};

/** Each flag is asked by the code that refuses the write it stands for. */
type ChatViewer = {
  canWrite: Served<boolean>;
  canLeave?: Served<boolean>;
  canClearHistory?: Served<boolean>;
};
