/**
 * Tooltip builder utilities for data tables.
 * Shared functions to build consistent tooltip text.
 */

interface SubscriberData {
  subscribersCount?: number;
  subscriberUsernames?: string[];
}

/**
 * Build tooltip for readers/subscribers list.
 * Shows "Prefix: user1, user2, user3" format.
 *
 * @param data - Object with subscribersCount and subscriberUsernames
 * @param emptyText - Text when count is 0 (e.g., "Нет читателей", "Нет подписчиков")
 * @param prefix - Prefix before names (e.g., "Читатели", "Подписчики")
 * @returns Tooltip text
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

/**
 * Build tooltip for game/blog readers.
 * Convenience wrapper for buildSubscribersTooltip with "readers" terminology.
 */
export function buildReadersTooltip(data: SubscriberData): string {
  return buildSubscribersTooltip(data, "Нет читателей", "Читатели");
}

/**
 * Build tooltip for likes list.
 * Shows "Оценили: user1, user2, user3" format.
 */
export function buildLikesTooltip(usernames: string[]): string {
  if (usernames.length === 0) return "";
  return `Оценили: ${usernames.join(", ")}`;
}

interface StatusByType {
  draft?: number;
  active?: number;
  closed?: number;
}

/**
 * Build status lines for tooltip (draft/active/closed counts).
 * @param byStatus - Object with draft, active, closed counts
 * @param suffix - Suffix for each line (e.g., "игры", "блоги")
 */
export function buildStatusLines(
  byStatus: StatusByType | undefined,
  suffix: string,
): string[] {
  if (!byStatus) return [];
  const lines: string[] = [];
  if (byStatus.draft && byStatus.draft > 0) {
    lines.push(`  Черновики: ${byStatus.draft} ${suffix}`);
  }
  if (byStatus.active && byStatus.active > 0) {
    lines.push(`  Активные: ${byStatus.active} ${suffix}`);
  }
  if (byStatus.closed && byStatus.closed > 0) {
    lines.push(`  Завершенные: ${byStatus.closed} ${suffix}`);
  }
  return lines;
}
