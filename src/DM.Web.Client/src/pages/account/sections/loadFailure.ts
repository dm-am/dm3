import type { GeneralError } from "@/shared/api/models/common";
import { describeFailureOrWait } from "@/shared/lib/errors";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

/**
 * The read of a section's state is its card's: a failure stands in place of the
 * state, so the card takes the refusal and the limit over from the interceptor.
 */
export const OWNED_BY_CARD = OWNS_REFUSAL;

/**
 * What the card says about a read that failed: the server's sentence, a wait
 * with its seconds, and `fallback` for an ended session and a request that
 * never left, which the interceptor speaks for.
 */
export function describeLoadFailure(
  failure: GeneralError,
  fallback: string,
): string {
  if (failure.status === 0 || failure.status === 401) return fallback;
  return describeFailureOrWait(failure, fallback);
}
