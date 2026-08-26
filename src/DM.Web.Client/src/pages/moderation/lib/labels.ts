// Russian display labels for moderation enums. This is the dictionary: the
// server keeps none, so there is nothing here to drift away from.
import {
  BanType,
  type Ban,
  type PremoderationStatus,
} from "@/entities/moderation";
import type { TicketStatus, TicketSubtype } from "@/entities/ticket";
import { pluralize } from "@/shared/lib/utils/pluralize";

// ==================== Ticket status ====================

/** Color class for the status indicator (doc 4.2.2.23). */
export const TICKET_STATUS_CLASSES: Record<TicketStatus, string> = {
  WaitingForModeration: "status-waiting-moderation",
  WaitingForUser: "status-waiting-user",
  Closed: "status-closed",
  Spam: "status-spam",
};

/** Sort weight: tickets waiting for moderation float to the top (doc 4.2.3.8.7/8). */
export const TICKET_STATUS_ORDER: Record<TicketStatus, number> = {
  WaitingForModeration: 0,
  WaitingForUser: 1,
  Closed: 2,
  Spam: 3,
};

// ==================== Ticket subtype ====================

/** Subtypes shown on the "Поддержка" page (doc 4.2.3.8.7, admin scope). */
export const SUPPORT_SUBTYPES: TicketSubtype[] = [
  "Bug",
  "AccessRecovery",
  "RegistrationIssue",
];

/** Subtypes shown on the "Жалобы" page (doc 4.2.3.8.8, moderator scope). */
export const COMPLAINT_SUBTYPES: TicketSubtype[] = [
  "UserComplaint",
  "ModeratorDecisionComplaint",
  "SeniorModeratorDecisionComplaint",
  "SiteImprovementSuggestion",
];

// ==================== Premoderation ====================

/** The wording of PremoderationStatus, and the only copy of it. */
export const PREMODERATION_STATUS_LABELS: Record<
  Exclude<PremoderationStatus, "Approved">,
  string
> = {
  AwaitingApproval: "Ожидает проверки",
  AwaitingEdits: "Требует правок",
};

// ==================== Warnings / bans ====================

/** "Предупреждение (N баллов)" / "Устное предупреждение (0 баллов)" (doc 4.2.2.21). */
export function warningTypeLabel(points: number): string {
  if (points === 0) return "Устное предупреждение (0 баллов)";
  const noun = pluralize(points, "балл", "балла", "баллов");
  return `Предупреждение (${points} ${noun})`;
}

export const BAN_TYPE_LABELS: Record<BanType, string> = {
  [BanType.Temporary]: "Временный",
  [BanType.Permanent]: "Постоянный",
  [BanType.Voluntary]: "Добровольный",
};

/** "N дней" / "Бессрочный" (doc 4.2.2.22). */
export function banTermLabel(ban: Ban): string {
  if (!ban.expiresUtc || ban.type === BanType.Permanent) return "Бессрочный";
  const started = new Date(ban.startedUtc).getTime();
  const expires = new Date(ban.expiresUtc).getTime();
  const days = Math.max(1, Math.round((expires - started) / 86_400_000));
  return `${days} ${pluralize(days, "день", "дня", "дней")}`;
}
