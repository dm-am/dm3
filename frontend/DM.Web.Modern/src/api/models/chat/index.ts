import type { User } from "@/api/models/community";

export type ChatMessageId = string;

export type ChatMessageEdit = {
  id: string;
  editedAtUtc: string;
  editor: User;
};

/**
 * Chat message model.
 * Note: Does not use Served<T> because store modifies these fields locally
 * (e.g., isRemoved, deletedBy when deleting).
 */
export type ChatMessage = {
  id: ChatMessageId;
  createdUtc: string;
  author: User;
  text: string;
  isRemoved: boolean;
  deletedBy: User | null;
  deletedAtUtc: string | null;
  likes: User[];
  edits: ChatMessageEdit[];
};
