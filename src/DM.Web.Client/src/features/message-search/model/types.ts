/**
 * Types for the search over a feed of messages: the global chat or one
 * correspondence.
 *
 * Mirrors the backend contract of GET /v1/search/messages
 * (MessageSearchController). The endpoint can address other sources as well,
 * but this client only ever asks it for the feed its field stands over.
 */

/**
 * Where a search looks: the global chat, or one correspondence ("dm:<chat id>").
 *
 * The endpoint can search every source the reader may read, but its entry
 * points are the fields above the two feeds, and a field above a feed searches
 * that feed. Anything wider needs a page of its own before it needs a control.
 */
export type MessageSearchScope = "global" | `dm:${string}`;

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
