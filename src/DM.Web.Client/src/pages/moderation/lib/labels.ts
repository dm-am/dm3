// Russian display labels for moderation enums (mirror the backend enum
// Description attributes — TicketStatus.cs / TicketSubtype.cs / BanType).
import type {
  PremoderationStatus,
  TicketStatus,
} from "@/shared/api/moderationApi";
import type { TicketSubtype } from "@/shared/api/supportApi";
import { BanType, type Ban } from "@/shared/api/moderationApi";

// ==================== Ticket status ====================

export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  WaitingForModeration: "Ожидает ответа модерации",
  WaitingForUser: "Ожидает ответа пользователя",
  Closed: "Закрыто",
  Spam: "Спам",
};

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

export const TICKET_SUBTYPE_LABELS: Record<TicketSubtype, string> = {
  UserComplaint: "Жалоба на пользователя",
  ModeratorDecisionComplaint: "Жалоба на решение младшего модератора",
  SeniorModeratorDecisionComplaint: "Жалоба на решение старшего модератора",
  SiteImprovementSuggestion: "Предложение по улучшению сайта",
  Bug: "Ошибка",
  AccessRecovery: "Восстановление доступа",
  RegistrationIssue: "Проблемы с регистрацией",
};

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

/** Mirrors the backend PremoderationStatus Description attributes. */
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
  return `Предупреждение (${points} ${pointsNoun(points)})`;
}

function pointsNoun(points: number): string {
  const mod10 = points % 10;
  const mod100 = points % 100;
  if (mod10 === 1 && mod100 !== 11) return "балл";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "балла";
  return "баллов";
}

export const BAN_TYPE_LABELS: Record<BanType, string> = {
  [BanType.Auto]: "Автоматический",
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
  return `${days} ${daysNoun(days)}`;
}

function daysNoun(days: number): string {
  const mod10 = days % 10;
  const mod100 = days % 100;
  if (mod10 === 1 && mod100 !== 11) return "день";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "дня";
  return "дней";
}
