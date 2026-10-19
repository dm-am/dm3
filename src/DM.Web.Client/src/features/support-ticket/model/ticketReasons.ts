import type { LocationQuery, RouteLocationRaw } from "vue-router";
import { TICKET_SUBTYPE_LABELS, type TicketSubtype } from "@/entities/ticket";
import { removedAccountRoute } from "@/entities/user";
import type { SelectOption } from "@/shared/ui/Select";

export type SupportTicketKind = "support" | "complaint";

// Support is "the site is in my way", a complaint "a person or a decision is"; ideas go to the forum.
const REASONS: Record<SupportTicketKind, TicketSubtype[]> = {
  support: [
    "Bug",
    "AccessRecovery",
    "RegistrationIssue",
    "AccountRemoval",
    "AccountRestoration",
    "PersonalData",
    "AgreementQuestion",
  ],
  complaint: [
    "UserComplaint",
    "ModeratorDecisionComplaint",
    "SeniorModeratorDecisionComplaint",
  ],
};

type ByReader<T> = T | { guest: T; member: T };

const APPEAL_HINT: ByReader<string> = {
  guest:
    "Напишите имя пользователя аккаунта, какое решение оспариваете и почему не согласны",
  member: "Напишите, какое решение оспариваете и почему не согласны",
};

// The facts the administration would otherwise ask for in a second round.
const REASON_HINTS: Partial<Record<TicketSubtype, ByReader<string>>> = {
  Bug: "Пожалуйста, скопируйте адрес страницы, на которой была замечена ошибка, а также постарайтесь указать, как можно воспроизвести ошибку",
  AccessRecovery: {
    guest:
      "Напишите имя пользователя и почту аккаунта, а также то, что знает только владелец: названия ваших игр и персонажей, примерную дату регистрации",
    member: "Напишите, что не получается и что при этом показывает сайт",
  },
  RegistrationIssue:
    "Напишите почту, на которую регистрируетесь, и что пошло не так",
  AccountRemoval:
    "Если удалить нужно не этот аккаунт, а другой, напишите его имя пользователя",
  AccountRestoration:
    "Напишите имя пользователя удаленного аккаунта и почту, к которой он был привязан",
  PersonalData:
    "Напишите, что сделать с вашими данными: показать, исправить, удалить или прекратить обработку",
  AgreementQuestion: "Напишите, какого пункта соглашения касается вопрос",
  UserComplaint:
    "Напишите, что произошло, а если это было в личных сообщениях, скопируйте сюда их текст",
  ModeratorDecisionComplaint: APPEAL_HINT,
  SeniorModeratorDecisionComplaint: APPEAL_HINT,
};

// A window is one the header opens on ?action=, over the page, so the form stays.
export type NoteLink =
  | { text: string; to: RouteLocationRaw }
  | { text: string; action: "recovery" | "two-factor" };

type NoteLine = {
  before: string;
  link?: NoteLink;
  after?: string;
};

export type Note = { lead?: NoteLine; lines: NoteLine[] };

const APPEAL_NOTE: Note = {
  lines: [
    {
      before: 'Кто из модераторов старший, видно в разделе "',
      link: {
        text: "Администрация",
        to: { name: "rules", hash: "#admins" },
      },
      after: '" правил',
    },
    {
      before:
        "Решения по жалобам принимаются коллегиально, хамство в жалобе добавляет баллы",
    },
  ],
};

// A reason with no note has no other way than this form.
const REASON_NOTES: Partial<Record<TicketSubtype, ByReader<Note>>> = {
  AccessRecovery: {
    guest: {
      lead: { before: "Сюда пишут, если нет доступа к почте аккаунта" },
      lines: [
        {
          before: "Забыли пароль или не пришло письмо активации: ",
          link: { text: "Восстановление доступа", action: "recovery" },
          after: " пришлет ссылку на почту",
        },
        {
          before: "Нет доступа к приложению и резервным кодам: ",
          link: { text: "Запрос на отключение", action: "two-factor" },
          after:
            " пришлет письмо, ссылка из него отключит двухфакторную аутентификацию через 7 дней",
        },
      ],
    },
    member: {
      lead: { before: "Вы уже вошли в аккаунт" },
      lines: [
        {
          before: "Пароль, почту и двухфакторную аутентификацию меняют в ",
          link: { text: "настройках аккаунта", to: { name: "account" } },
        },
      ],
    },
  },
  RegistrationIssue: {
    guest: {
      lead: {
        before:
          "Сюда пишут, если регистрация не проходит или письмо активации не приходит и после повторной отправки",
      },
      lines: [
        {
          before:
            'Письмо могло попасть в папку "Спам", отправить его еще раз можно через ',
          link: { text: "Восстановление доступа", action: "recovery" },
        },
      ],
    },
    member: { lines: [] },
  },
  AccountRemoval: {
    guest: {
      lead: { before: "Удаляем аккаунт только по просьбе из него самого" },
      lines: [
        {
          before:
            'Не получается войти: выберите причину "Восстановление доступа"',
        },
      ],
    },
    member: {
      lead: {
        before:
          'Посты, комментарии и сообщения останутся на месте, вместо имени над ними будет "Аккаунт удален"',
      },
      lines: [
        {
          before: 'Вернуть имя можно через поддержку, подробнее на странице "',
          link: { text: "Аккаунт удален", to: removedAccountRoute },
          after: '"',
        },
      ],
    },
  },
  UserComplaint: {
    guest: {
      lead: {
        before: "Жалобу на пользователя через форму подают из аккаунта",
      },
      lines: [
        {
          before:
            'Аккаунт в бане: выберите причину "Жалоба на решение старшего модератора"',
        },
      ],
    },
    member: { lines: [] },
  },
  ModeratorDecisionComplaint: APPEAL_NOTE,
  SeniorModeratorDecisionComplaint: APPEAL_NOTE,
};

// The server refuses these from a guest as well (CreateTicketIntakeValidator).
const MEMBER_ONLY: Partial<Record<TicketSubtype, string>> = {
  UserComplaint: "пожаловаться на пользователя",
  AccountRemoval: "запросить удаление аккаунта",
};

const forReader = <T>(
  value: ByReader<T> | undefined,
  isGuest: boolean,
): T | undefined => {
  if (value === undefined) return undefined;
  if (typeof value === "object" && value !== null && "guest" in value)
    return isGuest ? value.guest : value.member;
  return value as T;
};

export const reasonOptionsOf = (kind: SupportTicketKind): SelectOption[] =>
  REASONS[kind].map((value) => ({
    value,
    label: TICKET_SUBTYPE_LABELS[value],
  }));

// Only a reason this page offers is taken: a stale value would file the ticket under a hidden one.
export const requestedReasonOf = (
  kind: SupportTicketKind,
  query: LocationQuery,
): TicketSubtype | "" => {
  const requested = query.subtype;
  const name = Array.isArray(requested) ? requested[0] : requested;
  return REASONS[kind].find((reason) => reason === name) ?? "";
};

export const reasonHintOf = (
  subtype: TicketSubtype,
  isGuest: boolean,
): string => forReader(REASON_HINTS[subtype], isGuest) ?? "";

export const reasonNoteOf = (
  subtype: TicketSubtype,
  isGuest: boolean,
): Note | undefined => {
  const note = forReader(REASON_NOTES[subtype], isGuest);
  return note && (note.lead || note.lines.length) ? note : undefined;
};

export const memberOnlyAction = (subtype: TicketSubtype): string | undefined =>
  MEMBER_ONLY[subtype];
