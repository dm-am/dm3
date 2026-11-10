import { Api, unwrapResource, type RequestOptions } from "@/shared/api";
import type { Envelope } from "@/shared/api/models/common";
import { forumApi, type Topic, type TopicId } from "@/entities/forum";
import { globalChatApi, type GlobalChatMessage } from "@/entities/global-chat";
import type { Ban, Warning } from "@/entities/moderation";

// The only client of POST v1/moderation/warnings and POST v1/bans.

interface CreateWarningPayload {
  username: string;
  points: number;
  reason: string;
  entityId?: string;
  entityType?: string;
}

// The entity owns the union, its barrel exports it only as a field of Ban.
export type BanAccessPolicy = Ban["accessPolicy"];

// A non-voluntary ban must carry a term, so "Бессрочный" travels as 100 years (BanService's convention).
interface CreateBanPayload {
  username: string;
  durationHours?: number;
  expiresUtc?: string;
  comment: string;
  /** Required: the server has no default, and an omitted one is a 400. */
  accessPolicy: BanAccessPolicy;
}

export const PERMANENT_BAN_HOURS = 100 * 365 * 24;

// The scale a warning counts against, as the profile writes it ("N/6").
export const WARNING_LIMIT = 6;

export default new (class ModerationActionsApi {
  // A comment's text comes from its list; a topic's and a chat message's are fetched here.
  public async getQuotedText(
    entityType?: string,
    entityId?: string,
  ): Promise<string | null> {
    if (!entityId) return null;
    if (entityType === "Topic") {
      // A topic's text is its description.
      const { data } = await forumApi.getTopic(entityId as TopicId);
      return unwrapResource<Topic>(data)?.description || null;
    }
    if (entityType === "Message") {
      const { data } = await globalChatApi.getMessage(entityId);
      return unwrapResource<GlobalChatMessage>(data)?.text || null;
    }
    return null;
  }

  public createWarning(
    payload: CreateWarningPayload,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<Warning>>("moderation/warnings", payload, options);
  }

  public createBan(payload: CreateBanPayload, options?: RequestOptions) {
    return Api.post<Envelope<Ban>>("bans", payload, options);
  }
})();
