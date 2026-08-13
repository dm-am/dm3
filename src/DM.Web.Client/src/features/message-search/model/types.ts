/**
 * Types for the global-chat message search.
 *
 * Mirrors the backend contract of GET /v1/search/messages
 * (MessageSearchController). The endpoint can address other sources as well,
 * but this client only ever asks it for the global chat (see searchStore).
 */

/** Where a hit lives. `chat` covers both direct ("ЛС") and group chats. */
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
  /**
   * The preview, split into the runs a reader sees: a window around the match
   * with the match itself marked. [private] blocks are never in it.
   *
   * Segments and not a string of markup — the server marks the match, and a
   * marked string would be the one field of this contract that had to be rendered
   * as html. Printed as text nodes, the question does not arise.
   */
  snippet: SnippetSegment[];
};

/** One run of a search preview. */
export type SnippetSegment = {
  text: string;
  isMatch: boolean;
};

/** Query params sent to GET /v1/search/messages. */
export type SearchMessagesParams = {
  search: string;
  /** Repeatable scope filter, e.g. ["global"]. */
  in?: string[];
  cursor?: string;
  limit?: number;
};
