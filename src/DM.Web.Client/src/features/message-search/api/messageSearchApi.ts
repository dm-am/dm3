import type { CursorEnvelope } from "@/shared/api/models/common";
import type { MessageSearchResult, SearchMessagesParams } from "../model/types";
import { Api } from "@/shared/api";

export default new (class MessageSearchApi {
  /** Keyset paging: pass `paging.nextCursor` back as `cursor` for the next page. */
  public searchMessages(params: SearchMessagesParams) {
    return Api.get<CursorEnvelope<MessageSearchResult>>("search/messages", {
      search: params.search,
      in: params.in,
      cursor: params.cursor,
      limit: params.limit,
    });
  }
})();
