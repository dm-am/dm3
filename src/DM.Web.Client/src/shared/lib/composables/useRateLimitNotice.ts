/**
 * A rate limit standing in the form that earned it (ownsRateLimit): the
 * sentence in its strip, the submit shut until the wait is out, the number
 * falling each second.
 */
import { computed, onBeforeUnmount, ref, type ComputedRef } from "vue";
import type { GeneralError } from "@/shared/api/models/common";
import { describeRateLimit } from "@/shared/lib/errors";

/** As much of a failed request as the notice reads. */
type Failure = Pick<GeneralError, "status" | "retryAfterSeconds">;

/** What a form needs from a rate limit it is showing itself. */
export type RateLimitNotice = {
  /** The sentence for the form's own strip, empty while there is no limit. */
  message: ComputedRef<string>;
  /** Whether the submit control has to stay shut. */
  waiting: ComputedRef<boolean>;
  /**
   * Takes a failed request over if it is a wait, and says whether it was: a
   * 429, or whatever `isWait` names (the password lockout is a 403).
   */
  take: (failure: Failure, isWait?: boolean) => boolean;
  /** Drops the notice and its timer: the dialog closed, the form was reset. */
  clear: () => void;
};

export function useRateLimitNotice(): RateLimitNotice {
  /** Seconds still to wait. Zero means no wait is being counted. */
  const secondsLeft = ref(0);
  /** Whether a limit is being shown at all: it can be shown without a wait. */
  const refused = ref(false);

  let timer: ReturnType<typeof setInterval> | null = null;

  const stopTimer = () => {
    if (timer !== null) {
      clearInterval(timer);
      timer = null;
    }
  };

  const clear = () => {
    stopTimer();
    secondsLeft.value = 0;
    refused.value = false;
  };

  const take = (failure: Failure, isWait = false) => {
    if (failure.status !== 429 && !isWait) return false;

    // A second refusal replaces the first: two intervals would count double.
    stopTimer();
    refused.value = true;
    secondsLeft.value = failure.retryAfterSeconds ?? 0;

    // No header, no countdown: a wait the client invented would be a guess.
    if (secondsLeft.value > 0) {
      timer = setInterval(() => {
        secondsLeft.value -= 1;
        if (secondsLeft.value <= 0) clear();
      }, 1000);
    }

    return true;
  };

  // A form taken off the page mid-countdown; a dialog that closes clears it itself.
  onBeforeUnmount(clear);

  return {
    message: computed(() =>
      refused.value ? describeRateLimit(secondsLeft.value || undefined) : "",
    ),
    waiting: computed(() => secondsLeft.value > 0),
    take,
    clear,
  };
}
