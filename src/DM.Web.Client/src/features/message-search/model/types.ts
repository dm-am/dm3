/**
 * Types for the unified message + game-post search feature.
 *
 * Mirrors the backend contract of GET /v1/search/messages
 * (MessageSearchController). Each result row carries a discriminated source
 * tag plus the minimal identifying fields; the Russian UI labels
 * ("Глобальный чат" / "ЛС с X" / "Игра Y") are assembled on the client.
 */
import type { User } from "@/shared/api/models/common";

/** Where a hit lives. `chat` covers both direct (ЛС) and group chats. */
export type MessageSearchSourceType = "global" | "chat" | "game";

/**
 * A single search hit.
 *
 * `sourceId` links via the existing chat/game routes:
 *   - global/chat -> chatId
 *   - game        -> gameId
 * `id` is the messageId (global/chat) or postId (game), used for
 * jump-to-context.
 *
 * `author` is not part of the current backend contract; it is declared
 * optional so the card can render it the moment the API starts returning it,
 * without a type change.
 */
export type MessageSearchResult = {
  sourceType: MessageSearchSourceType;
  sourceId: string;
  /** Chat/group or game title; null/empty for direct chats (counterpart derived). */
  sourceTitle: string | null;
  id: string;
  createdUtc: string;
  /** [private]-stripped preview, already truncated server-side. */
  snippet: string;
  author?: User;
};

/** Which corpus the search runs against. */
export type SearchScopeKind = "all" | "global" | "dm" | "game";

/**
 * Scope selection. For `dm`/`game` a concrete target (chat/game) is required —
 * the backend `in:` filter addresses a single chat/game, there is no aggregate
 * "all DMs" / "all games" value.
 */
export type SearchScope = {
  kind: SearchScopeKind;
  /** chatId (dm) or gameId (game); undefined until a target is picked. */
  targetId?: string;
  /** Human label for the picked target (for the control's current-value text). */
  targetLabel?: string;
};

/**
 * Result ordering.
 *  - "date": newest first (the backend default).
 *  - "best": relevance (best full-text match). Sent as an optional `sort`
 *    query param; the backend ignores it until it grows relevance support,
 *    so the toggle degrades gracefully to date order.
 */
export type SearchSort = "date" | "best";

/** Query params sent to GET /v1/search/messages. */
export type SearchMessagesParams = {
  q: string;
  /** Repeatable scope filter, e.g. ["global"] or ["dm:<chatId>"]. */
  in?: string[];
  sort?: string;
  cursor?: string;
  limit?: number;
};
