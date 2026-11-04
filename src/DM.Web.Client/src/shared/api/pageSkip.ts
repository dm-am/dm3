/** The offset the wire takes for a 1-based page; none for the first, which it reads as zero. */
export function pageSkip(number: number, size: number): number | undefined {
  return number > 1 ? (number - 1) * size : undefined;
}
