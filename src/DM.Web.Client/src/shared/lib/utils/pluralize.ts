/**
 * Russian pluralization helper
 * @param count - number to pluralize
 * @param one - form for 1 (пост)
 * @param few - form for 2-4 (поста)
 * @param many - form for 5-20, 0 (постов)
 */
export function pluralize(
  count: number,
  one: string,
  few: string,
  many: string,
): string {
  const abs = Math.abs(count);
  const mod10 = abs % 10;
  const mod100 = abs % 100;

  if (mod100 >= 11 && mod100 <= 19) {
    return many;
  }

  if (mod10 === 1) {
    return one;
  }

  if (mod10 >= 2 && mod10 <= 4) {
    return few;
  }

  return many;
}

/**
 * Format count with pluralized word
 * @example formatCount(5, "пост", "поста", "постов") => "5 постов"
 */
export function formatCount(
  count: number,
  one: string,
  few: string,
  many: string,
): string {
  return `${count} ${pluralize(count, one, few, many)}`;
}
