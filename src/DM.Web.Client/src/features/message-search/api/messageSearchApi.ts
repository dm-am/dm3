import type { CursorEnvelope } from "@/shared/api/models/common";
import type { MessageSearchResult, SearchMessagesParams } from "../model/types";
import { Api } from "@/shared/api";

/**
 * Message + game-post search API.
 *
 * Hits the dedicated Postgres-backed endpoint GET /v1/search/messages
 * (separate from the OpenSearch-backed GET /v1/search used for
 * users/games/topics). Auth is required and the endpoint is rate limited
 * (sliding window) server-side.
 */
export default new (class MessageSearchApi {
  /**
   * Run a unified search over the global chat, the user's chats/DMs and the
   * game rooms they can read. Returns a keyset (forward-only, "load older")
   * cursor envelope; pass `paging.nextCursor` back as `cursor` for the next
   * page.
   */
  public searchMessages(params: SearchMessagesParams) {
    return Api.get<CursorEnvelope<MessageSearchResult>>("search/messages", {
      q: params.q,
      in: params.in,
      sort: params.sort,
      cursor: params.cursor,
      limit: params.limit ?? 50,
    });
  }
})();
