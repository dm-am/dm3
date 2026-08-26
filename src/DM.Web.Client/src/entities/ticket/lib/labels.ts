// Russian display labels for the ticket enums. This is the dictionary: the
// server keeps none, so there is nothing here to drift away from.
//
// They live with the entity rather than with the moderation pages because
// three screens in three different slices name the same statuses and
// subtypes — the moderation queues, "Мои обращения" and the public tracking
// page — and pages may not import each other.
import type { TicketStatus, TicketSubtype } from "../api";

export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  WaitingForModeration: "Ожидает ответа модерации",
  WaitingForUser: "Ожидает ответа пользователя",
  Closed: "Закрыто",
  Spam: "Спам",
};

export const TICKET_SUBTYPE_LABELS: Record<TicketSubtype, string> = {
  UserComplaint: "Жалоба на пользователя",
  ModeratorDecisionComplaint: "Жалоба на решение младшего модератора",
  SeniorModeratorDecisionComplaint: "Жалоба на решение старшего модератора",
  SiteImprovementSuggestion: "Предложение по улучшению сайта",
  Bug: "Ошибка",
  AccessRecovery: "Восстановление доступа",
  RegistrationIssue: "Проблемы с регистрацией",
};
