import { Api } from "@/shared/api";
import type { Envelope } from "@/shared/api/models/common";

/**
 * Moderation actions API — warning and ban creation dialogs.
 *
 * This is the only client for POST v1/moderation/warnings and POST v1/bans.
 * shared/api/moderationApi.ts used to declare the same two calls, typed as
 * bare `Warning`/`Ban` where the server returns `Envelope<...>`; nothing
 * called them and they have been removed. Do not add a second copy —
 * ownership of a file is not an architectural reason to duplicate a contract.
 *
 * Endpoint contracts mirror:
 * - POST v1/moderation/warnings (WarningController.CreateWarning, Moderator+)
 * - POST v1/bans       (BanController.CreateBan, SeniorModerator+)
 * - GET  v1/users/{username}/warnings (public points summary for the
 *   "Баллы: N/6" context line)
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
  isActive: boolean;
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
  isActive: boolean;
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
  type?: "Temporary" | "Permanent";
  durationHours?: number;
  expiresUtc?: string;
  comment: string;
  /** Ban access restriction scope, see {@link BanAccessPolicy}. */
  accessPolicy?: BanAccessPolicy;
}

/** 100 years in hours — the wire encoding of "Бессрочный". */
export const PERMANENT_BAN_HOURS = 100 * 365 * 24;

export default new (class ModerationActionsApi {
  /** Public warning points summary for the "Баллы: N/6" context line. */
  public getUserWarnings(username: string) {
    return Api.get<UserWarningsSummary>(
      `users/${encodeURIComponent(username)}/warnings`,
    );
  }

  /** Issue a warning (Moderator+). */
  public createWarning(payload: CreateWarningPayload) {
    return Api.post<Envelope<WarningResult>>("moderation/warnings", payload);
  }

  /** Issue a ban (SeniorModerator+). */
  public createBan(payload: CreateBanPayload) {
    return Api.post<Envelope<BanResult>>("bans", payload);
  }
})();
