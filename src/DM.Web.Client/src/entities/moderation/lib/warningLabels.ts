// The words a moderation record is read in. The server answers in names
// (WarningPlaceKind, BanType, BanAccessPolicy) and keeps no labels of its own
// (EnumCopyShould), so the dictionary is here, beside the cards that read it.
//
// Every decision of a warning is signed on one pattern, what, "от" whom, when,
// where the first part is a noun phrase that reads whole before "от" (the
// owner, 05.10: "Все должно выглядеть и читаться нормально со всеми от. И все
// должно быть унифицировано"): "Предупреждение (3 балла) от X", "Изменение
// баллов (1 балл) от X". A ban is written the way DM2's bot wrote its ban log
// (the owner, 12.10, line 45: "Баны надо в таком формате"), a sentence with the
// scope of the ban in it (BanLogCard.vue).
import { pluralize } from "@/shared/lib/utils/pluralize";
import {
  BanType,
  type BanAccessPolicy,
  type WarningPlaceKind,
} from "../api/moderationApi";

/** "3 балла": what the warning weighs, counted. */
export const pointsLabel = (points: number): string =>
  `${points} ${pluralize(points, "балл", "балла", "баллов")}`;

/**
 * The kind of the decision the warning is, the points it was issued with in
 * brackets beside it: "Предупреждение (3 балла)". A warning issued without
 * points is "Устное предупреждение" and says nothing of points (the owner,
 * 05.10: "Баллы пиши рядом с" the word "в скобочках, если это не устное
 * предупреждение"; 28.09: "устное предупреждение, а не комментарий").
 *
 * The points it was issued with, and not the ones it weighs now: a change of
 * them stands under it as a decision of its own, named by the points it left
 * (pointsChangeLabel), and between the two both numbers are read. Named by
 * the points it weighs now, 3 changed to 1 would read "1 балл" twice and lose
 * the 3.
 */
export const warningKindLabel = (points: number): string =>
  points > 0
    ? `Предупреждение (${pointsLabel(points)})`
    : "Устное предупреждение";

/**
 * "Изменение баллов (1 балл)": the change of the points, as the kind of its
 * decision, the points it left in brackets as a warning has its own (the
 * owner asked on 12.10 for "Изменение баллов (x баллов)"); a change to nothing
 * counts its nothing, "(0 баллов)". A noun, so that "от" after it names who
 * changed them; "Баллы ... от X" read as if the points had come from X.
 */
export const pointsChangeLabel = (points: number): string =>
  `Изменение баллов (${pointsLabel(points)})`;

/**
 * What the caption names the place by when the server sends no title: the
 * global chat always, a topic, game, blog or publication whose title is
 * missing.
 */
const PLACE_NAMES: Record<WarningPlaceKind, string> = {
  Topic: "Топик",
  Game: "Игра",
  Blog: "Блог",
  GlobalChat: "Чат",
  Publication: "Публикация",
};

/** The name of the place in the caption: its title, or its kind. */
export const placeLabel = (
  kind: WarningPlaceKind | null | undefined,
  title: string | null | undefined,
): string | null => {
  if (!kind) return null;
  if (kind === "GlobalChat") return PLACE_NAMES.GlobalChat;
  return title || PLACE_NAMES[kind];
};

/** What a ban is read by: its kind and the two ends of its term. */
type BanTerm = {
  type: BanType;
  startedUtc: string;
  expiresUtc?: string | null;
};

/** A ban that does not end: written so, or with no end at all. */
export const isPermanent = (ban: BanTerm): boolean =>
  ban.type === BanType.Permanent || !ban.expiresUtc;

/**
 * "7 дней" / "Бессрочный" (doc 4.2.2.22): how long a ban runs, in whole days
 * and at least one.
 */
export function banTermLabel(ban: BanTerm): string {
  if (isPermanent(ban)) return "Бессрочный";
  const started = new Date(ban.startedUtc).getTime();
  const expires = new Date(ban.expiresUtc as string).getTime();
  const days = Math.max(1, Math.round((expires - started) / 86_400_000));
  return `${days} ${pluralize(days, "день", "дня", "дней")}`;
}

/** What DM2's ban log calls the two scopes of a ban. */
const BAN_SCOPES: Record<BanAccessPolicy, string> = {
  DemocraticBan: "демократический бан",
  FullBan: "полный бан",
};

/**
 * The scope of a ban as DM2's ban log names it: "демократический бан",
 * "полный бан". The term is not in it, the entry ends with the end date
 * instead. A ban without an end is "бессрочный", a word the site already uses
 * ("Текущий бан: полный бессрочный"), and not a date five hundred years ahead
 * as on DM2: ours are kept for a hundred, a number nobody knows. A voluntary
 * ban has no words of its own: there is no such ban on the site any more, and
 * a row that still carries the flag reads as any other.
 */
export function banScopeLabel(
  ban: BanTerm & { accessPolicy: BanAccessPolicy },
): string {
  const scope = BAN_SCOPES[ban.accessPolicy] ?? BAN_SCOPES.FullBan;
  return isPermanent(ban) ? `бессрочный ${scope}` : scope;
}
