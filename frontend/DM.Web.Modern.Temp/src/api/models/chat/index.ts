import type { User } from "@/api/models/community";

export type ChatMessageId = string;

export type ChatMessage = {
  id: ChatMessageId;
  createdUtc: string;
  modifiedUtc: string | null;
  author: User;
  text: string;
  isRemoved: boolean;
  likes: User[];
};
