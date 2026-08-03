import type { CursorEnvelope } from "@/shared/api/models/common";
import type { MessageSearchResult, SearchMessagesParams } from "../model/types";
import { Api } from "@/shared/api";

/**
 * Message search API.
 *
 * Hits GET /v1/search/messages. Auth is required and the endpoint is rate limited
 * (sliding window) server-side.
 */
export default new (class MessageSearchApi {
  /**
   * Run a search over the sources named by `in`. Returns a keyset
   * (forward-only, "load older") cursor envelope; pass `paging.nextCursor`
   * back as `cursor` for the next page.
   */
  public searchMessages(params: SearchMessagesParams) {
    return Api.get<CursorEnvelope<MessageSearchResult>>("search/messages", {
      q: params.q,
      in: params.in,
      cursor: params.cursor,
      limit: params.limit ?? 50,
    });
  }
})();
