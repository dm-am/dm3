/**
 * A rate limit standing in the form that earned it, with the wait running down.
 * @module shared/lib/composables/useRateLimitNotice
 *
 * The refusal used to arrive as a toast and nothing else: it faded after eight
 * seconds, the form under it was unchanged — button live, strip empty — and the
 * reader pressed again, bought the same refusal, and concluded the form was
 * broken. The way in is precisely where that happens, because it is the one
 * screen a person retries on purpose.
 *
 * So the form takes the refusal over (`ownsRateLimit` on the request) and shows
 * it where it shows its own: the sentence in the strip it already reserves, the
 * submit control shut until the wait is out, and the number inside the sentence
 * falling a second at a time. No new word appears on screen — the caption of
 * the button does not change — because the only thing the reader has to learn
 * is how long, and that is the number.
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
   * Takes a failed request over if it is a wait, and says whether it was.
   * A caller reads it as "answered, nothing left to say about this failure".
   *
   * A 429 is a wait by its status alone. Anything else is one only if the
   * caller says so through `isWait`: the lockout after too many wrong
   * passwords arrives as a 403 and is the same event to the reader - a door
   * that opens again at a named time - but only the screen that knows the
   * refusal can tell it from the other 403s.
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

    // A second refusal replaces the first: its wait is the current one, and two
    // intervals on one counter would take it down two seconds at a time.
    stopTimer();
    refused.value = true;
    secondsLeft.value = failure.retryAfterSeconds ?? 0;

    // No header, no countdown: the sentence then names no number and the button
    // stays live, because a wait this side invented would be a guess enforced
    // on the reader.
    if (secondsLeft.value > 0) {
      timer = setInterval(() => {
        secondsLeft.value -= 1;
        if (secondsLeft.value <= 0) clear();
      }, 1000);
    }

    return true;
  };

  // A dialog that closes leaves the component mounted, so the timer outlives
  // the screen it belongs to unless the form clears it as well; this covers the
  // other half, a form actually taken off the page mid-countdown.
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
