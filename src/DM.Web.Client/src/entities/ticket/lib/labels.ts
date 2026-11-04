// The server keeps no labels; three slices name the same statuses, so the words live here.
import type { TicketStatus, TicketSubtype } from "../api";

// Both sides write in a ticket, so a status says whose answer it waits for.
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
  Bug: "Ошибка",
  AccessRecovery: "Восстановление доступа",
  RegistrationIssue: "Проблемы с регистрацией",
  AccountRemoval: "Удаление аккаунта",
  AccountRestoration: "Восстановление удаленного аккаунта",
  PersonalData: "Персональные данные",
  AgreementQuestion: "Вопрос по соглашению",
};
