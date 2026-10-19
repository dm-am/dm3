// The wire names a review's sign ("Positive"), the rating counts it and the forms send it as a number.
import type {
  ReviewSign,
  ReviewSignName,
} from "@/shared/api/models/game/reviews";

/** "Positive" | 1 | "1" → 1. Anything unrecognised is neutral. */
export function reviewSignToNumber(
  sign?: ReviewSign | string | number,
): number {
  const spelled = String(sign);
  if (spelled === "Positive" || spelled === "1") return 1;
  if (spelled === "Negative" || spelled === "-1") return -1;
  return 0;
}

/** 1 → "Positive", the name the wire sends. */
export function reviewSignName(sign: number): ReviewSignName {
  return sign > 0 ? "Positive" : sign < 0 ? "Negative" : "Neutral";
}

export interface ReviewSignOption {
  value: "1" | "0" | "-1";
  label: string;
}

// A newbie rates neutral only: the server takes "+" and "−" from 100 posts on.
export function reviewSignOptions(canSign: boolean): ReviewSignOption[] {
  const neutral: ReviewSignOption = { value: "0", label: "=" };
  return canSign
    ? [{ value: "1", label: "+" }, neutral, { value: "-1", label: "−" }]
    : [neutral];
}
