import { pluralize } from "./pluralize";

interface SubscriberData {
  subscribersCount?: number;
  subscriberUsernames?: string[];
}

/**
 * "Prefix: user1, user2, user3" for a list of readers or subscribers.
 *
 * @param data - Object with subscribersCount and subscriberUsernames
 * @param emptyText - Text when count is 0 (e.g., "Нет читателей", "Нет подписчиков")
 * @param prefix - Prefix before names (e.g., "Читатели", "Подписчики")
 */
export function buildSubscribersTooltip(
  data: SubscriberData,
  emptyText = "Нет подписчиков",
  prefix = "Подписчики",
): string {
  const names = data.subscriberUsernames ?? [];
  const total = data.subscribersCount ?? 0;

  if (total === 0) return emptyText;
  if (names.length === 0) return `${prefix}: ${total}`;
  if (names.length < total) {
    return `${prefix}: ${names.join(", ")}... и еще ${total - names.length}`;
  }
  return `${prefix}: ${names.join(", ")}`;
}

export function buildReadersTooltip(data: SubscriberData): string {
  return buildSubscribersTooltip(data, "Нет читателей", "Читатели");
}

interface StatusByType {
  draft?: number;
  active?: number;
  closed?: number;
}

/** Russian plural forms tuple: [one, few, many] (e.g. ["игра", "игры", "игр"]). */
export type PluralForms = [one: string, few: string, many: string];

/**
 * The draft/active/closed lines of a tooltip.
 *
 * @param byStatus - Object with draft, active, closed counts
 * @param forms - `PluralForms` tuple, e.g. `["игра", "игры", "игр"]`
 */
export function buildStatusLines(
  byStatus: StatusByType | undefined,
  forms: PluralForms,
): string[] {
  if (!byStatus) return [];

  const wordFor = (count: number): string => pluralize(count, ...forms);

  const lines: string[] = [];
  if (byStatus.draft && byStatus.draft > 0) {
    lines.push(`  Оформляется: ${byStatus.draft} ${wordFor(byStatus.draft)}`);
  }
  if (byStatus.active && byStatus.active > 0) {
    lines.push(`  Активные: ${byStatus.active} ${wordFor(byStatus.active)}`);
  }
  if (byStatus.closed && byStatus.closed > 0) {
    lines.push(`  Закрытые: ${byStatus.closed} ${wordFor(byStatus.closed)}`);
  }
  return lines;
}
