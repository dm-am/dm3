/**
 * The two spellings of a post review's sign.
 *
 * The wire carries the name the enum is declared with ("Positive"), while the
 * rating a post shows is arithmetic over +1 / 0 / -1, and the create and edit
 * forms send the number back. Both directions used to be written out wherever
 * they were needed, and a component that wrote a number into a field holding a
 * name turned "+1" into "+0" without any error to notice.
 */
import type { ReviewSign } from "@/shared/api/models/game/reviews";

/** "Positive" | 1 | "1" → 1. Anything unrecognised is neutral. */
export function reviewSignToNumber(
  sign?: ReviewSign | string | number,
): number {
  const spelled = String(sign);
  if (spelled === "Positive" || spelled === "1") return 1;
  if (spelled === "Negative" || spelled === "-1") return -1;
  return 0;
}

/** 1 → "Positive". The spelling every read path of a review expects. */
export function reviewSignName(sign: number): string {
  if (sign > 0) return "Positive";
  if (sign < 0) return "Negative";
  return "Neutral";
}
