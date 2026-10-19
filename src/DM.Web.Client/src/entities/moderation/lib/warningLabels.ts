// The server answers in names and keeps no labels, so the words are here.
// A decision is signed "what от whom", the first part a noun phrase that reads whole.
import { pluralize } from "@/shared/lib/utils/pluralize";
import { BanType, type BanAccessPolicy } from "../api/banApi";
import type { WarningPlaceKind } from "../api/warningApi";

const pointsLabel = (points: number): string =>
  `${points} ${pluralize(points, "балл", "балла", "баллов")}`;

// By the points it was issued with: a change stands under it, so both numbers are read.
export const warningKindLabel = (points: number): string =>
  points > 0
    ? `Предупреждение (${pointsLabel(points)})`
    : "Устное предупреждение";

// A noun, so that "от" names who changed them and not where the points came from.
export const pointsChangeLabel = (points: number): string =>
  `Изменение баллов (${pointsLabel(points)})`;

/** For a place the server sends no title for. */
const PLACE_NAMES: Record<WarningPlaceKind, string> = {
  Topic: "Топик",
  Game: "Игра",
  Blog: "Блог",
  GlobalChat: "Чат",
  Publication: "Публикация",
};

export const placeLabel = (
  kind: WarningPlaceKind | null | undefined,
  title: string | null | undefined,
): string | null => {
  if (!kind) return null;
  if (kind === "GlobalChat") return PLACE_NAMES.GlobalChat;
  return title || PLACE_NAMES[kind];
};

type BanTerm = {
  type: BanType;
  startedUtc: string;
  expiresUtc?: string | null;
};

export const isPermanent = (ban: BanTerm): boolean =>
  ban.type === BanType.Permanent || !ban.expiresUtc;

/** "7 дней" or "Бессрочный", in whole days and at least one. */
export function banTermLabel(ban: BanTerm): string {
  if (isPermanent(ban)) return "Бессрочный";
  const started = new Date(ban.startedUtc).getTime();
  const expires = new Date(ban.expiresUtc as string).getTime();
  const days = Math.max(1, Math.round((expires - started) / 86_400_000));
  return `${days} ${pluralize(days, "день", "дня", "дней")}`;
}

/** As DM2's ban log calls them. */
const BAN_SCOPES: Record<BanAccessPolicy, string> = {
  DemocraticBan: "демократический бан",
  FullBan: "полный бан",
};

// A ban without an end is "бессрочный", not a date a hundred years ahead.
export function banScopeLabel(
  ban: BanTerm & { accessPolicy: BanAccessPolicy },
): string {
  const scope = BAN_SCOPES[ban.accessPolicy] ?? BAN_SCOPES.FullBan;
  return isPermanent(ban) ? `бессрочный ${scope}` : scope;
}
