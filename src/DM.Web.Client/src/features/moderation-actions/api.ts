import { Api, unwrapResource } from "@/shared/api";
import type { Envelope } from "@/shared/api/models/common";
import { forumApi, type Topic, type TopicId } from "@/entities/forum";
import { globalChatApi, type GlobalChatMessage } from "@/entities/global-chat";

/**
 * Moderation actions API — warning and ban creation dialogs.
 *
 * This is the only client for POST v1/moderation/warnings and POST v1/bans.
 * The moderation client (now entities/moderation/api) used to declare the same
 * two calls, typed as bare `Warning`/`Ban` where the server returns
 * `Envelope<...>`; nothing called them and they have been removed. Do not add a
 * second copy — ownership of a file is not an architectural reason to duplicate
 * a contract.
 *
 * Endpoint contracts mirror:
 * - POST v1/moderation/warnings (WarningController.CreateWarning, Moderator+)
 * - POST v1/bans       (BanController.CreateBan, SeniorModerator+)
 * - GET  v1/users/{username}/warnings (public points summary for the
 *   "Нарушения: N/6" line of the warning window)
 * - GET  v1/topics/{id} and v1/global-chat/messages/{id} (through forumApi and
 *   globalChatApi), the text the warning window quotes
 */

/** Warning as returned by the moderator-only create endpoint. */
export interface WarningResult {
  id: string;
  user?: { username: string };
  moderator?: { username: string };
  entityId?: string;
  entityType?: string;
  points: number;
  reason: string;
  createdUtc: string;
}

/**
 * POST v1/moderation/warnings payload (CreateWarningRequest).
 * Backend validation (CreateWarningValidator): username required,
 * points 0-6 inclusive (0 = verbal), reason required + max 2000,
 * entityType max 100.
 */
export interface CreateWarningPayload {
  username: string;
  points: number;
  reason: string;
  entityId?: string;
  entityType?: string;
}

/** Aggregate warning facts visible to everyone (no reason, no moderator). */
export interface PublicWarningInfo {
  points: number;
  createdUtc: string;
}

/** GET v1/users/{username}/warnings response (UserWarningsInfo). */
export interface UserWarningsSummary {
  username: string;
  totalPoints: number;
  activeCount: number;
  warnings: PublicWarningInfo[];
}

/** Ban as returned by the moderator-only create endpoint. */
export interface BanResult {
  id: string;
  user?: { username: string };
  moderator?: { username: string };
  type: string;
  startedUtc: string;
  expiresUtc?: string;
  comment: string;
  isActive: boolean;
}

/**
 * Ban access restriction policy per the product doc (4.2.4.2):
 * "Демократический" keeps read access, "Полный" blocks everything. Sent as
 * `accessPolicy` on CreateBanRequest and persisted by BanService.CreateBan;
 * anything other than these two is coerced to FullBan server-side.
 */
export type BanAccessPolicy = "DemocraticBan" | "FullBan";

/**
 * POST v1/bans payload (CreateBanRequest).
 * Backend validation (CreateBanValidator): username required, comment
 * required + max 2000, durationHours > 0 when present; a non-voluntary ban
 * MUST carry durationHours or expiresUtc — permanent bans are therefore
 * expressed as a 100-year duration, matching the domain's own permanent
 * convention (BanService stores permanent as now + 100 years and treats
 * anything beyond 50 years as permanent).
 */
export interface CreateBanPayload {
  username: string;
  durationHours?: number;
  expiresUtc?: string;
  comment: string;
  /**
   * Ban access restriction scope, see {@link BanAccessPolicy}. Required: the
   * server has no default for it, and an omitted scope is a 400 rather than the
   * strictest ban.
   */
  accessPolicy: BanAccessPolicy;
}

/** 100 years in hours — the wire encoding of "Бессрочный". */
export const PERMANENT_BAN_HOURS = 100 * 365 * 24;

/**
 * The refusal a window takes over: the warning and the ban dialogs print a
 * refused request in themselves, and the response interceptor has to stay
 * quiet or the same sentence stands in two places.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The window shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The window shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

export default new (class ModerationActionsApi {
  /** Public warning points summary for the "Нарушения: N/6" line. */
  public getUserWarnings(username: string) {
    return Api.get<UserWarningsSummary>(
      `users/${encodeURIComponent(username)}/warnings`,
    );
  }

  /**
   * The text a warning is issued for, rendered as every reader sees it, for
   * the window to quote: a topic, a message of the global chat. Their openers
   * hand over the address and the id alone, so the window asks for the text
   * itself. A comment comes with its text from the list that holds it
   * (useCommentWarnDialog), and a profile is the page the window stands on.
   * Null where there is nothing to quote or the text did not come.
   */
  public async getQuotedText(
    entityType?: string,
    entityId?: string,
  ): Promise<string | null> {
    if (!entityId) return null;
    if (entityType === "Topic") {
      // The text of a topic is its description, the first message of it.
      const { data } = await forumApi.getTopic(entityId as TopicId);
      return unwrapResource<Topic>(data)?.description || null;
    }
    if (entityType === "Message") {
      const { data } = await globalChatApi.getMessage(entityId);
      return unwrapResource<GlobalChatMessage>(data)?.text || null;
    }
    return null;
  }

  /** Issue a warning (Moderator+). */
  public createWarning(payload: CreateWarningPayload, options?: RefusalFlags) {
    return Api.post<Envelope<WarningResult>>(
      "moderation/warnings",
      payload,
      options,
    );
  }

  /** Issue a ban (SeniorModerator+). */
  public createBan(payload: CreateBanPayload, options?: RefusalFlags) {
    return Api.post<Envelope<BanResult>>("bans", payload, options);
  }
})();
