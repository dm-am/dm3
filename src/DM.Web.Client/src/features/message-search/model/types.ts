/**
 * Types for the global-chat message search.
 *
 * Mirrors the backend contract of GET /v1/search/messages
 * (MessageSearchController). The endpoint can address other sources as well,
 * but this client only ever asks it for the global chat (see searchStore).
 */

/** Where a hit lives. `chat` covers both direct (ЛС) and group chats. */
export type MessageSearchSourceType = "global" | "chat" | "game";

/**
 * A single search hit.
 *
 * `sourceId` is the chat id (global/chat) or the game id (game). `id` is the
 * message id (global/chat) or the post id (game), used for jump-to-context.
 */
export type MessageSearchResult = {
  sourceType: MessageSearchSourceType;
  sourceId: string;
  /** Chat/group or game title; null/empty for direct chats. */
  sourceTitle: string | null;
  id: string;
  createdUtc: string;
  /** [private]-stripped preview, already truncated server-side. */
  snippet: string;
};

/** Query params sent to GET /v1/search/messages. */
export type SearchMessagesParams = {
  q: string;
  /** Repeatable scope filter, e.g. ["global"]. */
  in?: string[];
  cursor?: string;
  limit?: number;
};
