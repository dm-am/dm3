// Russian display labels for the ticket enums. This is the dictionary: the
// server keeps none, so there is nothing here to drift away from.
//
// They live with the entity rather than with the moderation pages because
// three screens in three different slices name the same statuses and
// subtypes — the moderation queues, "Мои обращения" and the public tracking
// page — and pages may not import each other.
import type { TicketStatus, TicketSubtype } from "../api";

/**
 * Where a ticket stands, as its author, a guest and a moderator read it alike:
 * whose turn it is, or how it ended. The words it had before 5 October (the
 * owner saw them and did not object): once both sides write in a ticket, a
 * "Ждет ответа" that does not say whose answer it waits for is not true, and
 * the state of a ticket somebody took in hand went with the taking (owner,
 * 12.10, line 51). None of them has a colour of its own: a state is a word of
 * the grey caption.
 */
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
  AccountRemoval: "Удаление аккаунта",
  AccountRestoration: "Восстановление удаленного аккаунта",
  PersonalData: "Персональные данные",
  AgreementQuestion: "Вопрос по соглашению",
};
