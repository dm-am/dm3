/**
 * Fixtures for the DEV-ONLY events-strip catalog (/dev/chat-events).
 *
 * No API calls: the catalog is judged on layout, and a page that had to be
 * signed in and seeded would show a different line on every stand.
 *
 * Times are literal strings rather than dayjs output on purpose. A catalog is
 * read over several days, and a computed "до 22:48" would quietly disappear
 * once the clock passed it — which is exactly what the production strip does
 * today, because it derives the end from the planned start plus the duration
 * and the API sends no actual start.
 *
 * The description is stored as rendered HTML because that is the shape the
 * events endpoint returns. A plain-text fixture would understate the height of
 * the card every disclosure variant opens.
 */
import type { SegmentedOption } from "@/shared/ui/SegmentedControl";

/** The three members of the domain enum, spelled as the API spells them. */
export type MockStatus = "Scheduled" | "Live" | "Ended";

/**
 * One event as the strip sees it: the summary fields the list endpoint sends,
 * with the muted tail pre-formatted per state.
 */
export interface MockEvent {
  id: string;
  title: string;
  /** Muted tail after the title, already formatted for this state. */
  timeText: string;
  status: MockStatus;
  /** Open events take anyone, closed ones only their participants. */
  isOpen: boolean;
  participantCount: number;
  /** DD.MM, for the "ближайший" tail of the count. */
  startsShort: string;
}

/**
 * The word every state gets. Today only Live carries one, so a scheduled event
 * reads as a running one and the only difference on screen is the font weight.
 */
export const STATE_WORD: Record<MockStatus, string> = {
  Live: "Идет",
  Scheduled: "Скоро",
  Ended: "Закончился",
};

const LIVE: MockEvent = {
  id: "live",
  title: "Вечер быстрых зарисовок",
  timeText: "до 22:48",
  status: "Live",
  isOpen: true,
  participantCount: 12,
  startsShort: "04.08",
};

const LIVE_CLOSED: MockEvent = {
  ...LIVE,
  id: "live-closed",
  isOpen: false,
  participantCount: 6,
};

const ENDED: MockEvent = {
  ...LIVE,
  id: "ended",
  timeText: "сегодня в 21:30",
  status: "Ended",
};

const SOON_ONE: MockEvent = {
  id: "soon-one",
  title: "Разбор чужих партий",
  timeText: "05.08 в 19:00",
  status: "Scheduled",
  isOpen: true,
  participantCount: 4,
  startsShort: "05.08",
};

const SOON_TWO: MockEvent = {
  id: "soon-two",
  title: "Читка по ролям, глава третья",
  timeText: "07.08 в 20:00",
  status: "Scheduled",
  isOpen: false,
  participantCount: 9,
  startsShort: "07.08",
};

const SOON_THREE: MockEvent = {
  id: "soon-three",
  title: "Мастерская: как водить новичков",
  timeText: "11.08 в 19:30",
  status: "Scheduled",
  isOpen: true,
  participantCount: 3,
  startsShort: "11.08",
};

export type ScenarioId =
  | "empty"
  | "one"
  | "live"
  | "live-closed"
  | "live-plus-two"
  | "three-ahead"
  | "ended";

/**
 * One switch feeds every variant at once, so a single click shows how all of
 * them behave in the same state, including the ones that jump.
 */
export const SCENARIOS: Record<ScenarioId, MockEvent[]> = {
  empty: [],
  one: [SOON_ONE],
  live: [LIVE],
  "live-closed": [LIVE_CLOSED],
  "live-plus-two": [LIVE, SOON_ONE, SOON_TWO],
  "three-ahead": [SOON_ONE, SOON_TWO, SOON_THREE],
  ended: [ENDED, SOON_ONE],
};

export const SCENARIO_OPTIONS: readonly SegmentedOption<ScenarioId>[] = [
  { value: "empty", label: "нет эвентов" },
  { value: "one", label: "один впереди" },
  { value: "live", label: "идет" },
  { value: "live-closed", label: "идет закрытый" },
  { value: "live-plus-two", label: "идет и два впереди" },
  { value: "three-ahead", label: "три впереди" },
  { value: "ended", label: "закончился" },
];

/**
 * Note on the "закончился" scenario: it does not exist on the live site. The
 * service asks the repository for Live and Scheduled only, so an ended event
 * never reaches the client. Whether it should is one of the questions this
 * catalog is meant to settle, and a state cannot be judged unseen.
 */

export type FrameWidth = "full" | "768" | "480" | "375";

export const WIDTH_OPTIONS: readonly SegmentedOption<FrameWidth>[] = [
  { value: "full", label: "полная" },
  { value: "768", label: "768" },
  { value: "480", label: "480" },
  { value: "375", label: "375" },
];

export type ViewerId = "guest" | "user" | "member" | "organizer";

export const VIEWER_OPTIONS: readonly SegmentedOption<ViewerId>[] = [
  { value: "guest", label: "гость" },
  { value: "user", label: "пользователь" },
  { value: "member", label: "участник" },
  { value: "organizer", label: "организатор" },
];

export type RulerMode = "off" | "on";

export const RULER_OPTIONS: readonly SegmentedOption<RulerMode>[] = [
  { value: "off", label: "скрыта" },
  { value: "on", label: "показана" },
];

/** Details the list endpoint does not send: they arrive with the card. */
export interface MockDetails {
  organizer: string;
  participants: string[];
  /** Rendered HTML, as the events endpoint returns it. */
  description: string;
}

export const DETAILS: MockDetails = {
  organizer: "SolohinLex",
  participants: ["Astrellan", "Miriamel", "GrayWanderer"],
  description:
    "<strong>Три подхода по двадцать минут.</strong><br />Пишем короткие сцены по общей завязке, потом разбираем их вслух.<ul><li>Готовиться заранее не нужно</li><li>Сцена не длиннее пятнадцати строк</li></ul>",
};

/**
 * What the action item offers this viewer, or null when it offers nothing.
 * The client API already has join, leave, start and end with zero callers, so
 * this is a question about whether the strip should grow actions at all.
 */
export function actionFor(
  viewer: ViewerId,
  event: MockEvent | null,
): string | null {
  if (!event || event.status === "Ended") return null;
  if (viewer === "member") return "выйти";
  if (viewer === "organizer")
    return event.status === "Live" ? "закончить" : "начать";
  if (viewer === "user") return event.isOpen ? "участвовать" : null;
  return null;
}

/** Why there is no action, when there is none and the reason is worth a word. */
export function actionNoteFor(
  viewer: ViewerId,
  event: MockEvent | null,
): string | null {
  if (!event || event.status === "Ended") return null;
  if (viewer === "guest") return "войдите, чтобы участвовать";
  if (viewer === "user" && !event.isOpen)
    return "закрытый эвент, только по приглашению";
  return null;
}

/** One row of the feed replica behind the strip. */
export interface MockFeedRow {
  id: string;
  author: string;
  time: string;
  text: string;
  /** Same author as the row above: no avatar, no header. */
  isContinuation: boolean;
}

export const FEED: MockFeedRow[] = [
  {
    id: "m1",
    author: "Astrellan",
    time: "21:02",
    text: "Кто-нибудь помнит, чем закончилась прошлая партия в Тенях старого города?",
    isContinuation: false,
  },
  {
    id: "m2",
    author: "GrayWanderer",
    time: "21:04",
    text: "Мы застряли в подвале, и мастер объявил перерыв на неделю.",
    isContinuation: false,
  },
  {
    id: "m3",
    author: "GrayWanderer",
    time: "21:05",
    text: "Ключ, кажется, так и остался у трактирщика.",
    isContinuation: true,
  },
  {
    id: "m4",
    author: "Miriamel",
    time: "21:07",
    text: "Я записалась на вечер зарисовок, там как раз обещали разбирать сцены.",
    isContinuation: false,
  },
];
