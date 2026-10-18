import { computed, ref, type ComputedRef } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import {
  describeFailure,
  refusalOfTheReader,
  takeOverRefusal,
  type FieldLabels,
} from "@/shared/lib/errors";
import { useRateLimitNotice } from "./useRateLimitNotice";

/** What a place on the page says about the last press made there. */
export interface ActionResult {
  /** The refusal line, empty while nothing was refused. */
  refusal: ComputedRef<string>;
  /** The server asked to wait: the control stays shut until zero. */
  waiting: ComputedRef<boolean>;
  /**
   * Writes a refusal at the place and takes down the plate the interceptor
   * held for it. An expired session and a request that never arrived are left
   * to the plate, and the line stays empty.
   */
  refuse: (
    error: GeneralError | BadRequestError,
    fallback: string,
    labels?: FieldLabels,
  ) => void;
  /** Empties the line: the place is pressed again, or it changed. */
  clear: () => void;
}

/**
 * The refusal of a press, written where the press was made.
 *
 * A press that failed used to answer with a plate in the corner of the screen,
 * away from the button, the row or the form it was about, while the place
 * itself stood unchanged. The owner's rule of 26 September 2026 keeps a plate
 * only where the page has nowhere to say it; everywhere else the answer is a
 * line at the place, drawn by ActionResult (shared/ui/ActionResult).
 *
 * One of these per place: a form's button, a row of a list, a control of a
 * card. The line holds until the place is pressed again (`clear`), so a reader
 * who looked away still finds it. A rate limit stands in the line with the
 * wait counting down, and `waiting` keeps the control shut until it is out,
 * as the guest forms and the windows do (useRateLimitNotice).
 *
 * The wording is describeFailure's, as in a window: the server's own sentence
 * where it gave one, the fields it refused by their labels on the screen, and
 * the fallback where it named nothing.
 */
export function useActionResult(): ActionResult {
  const said = ref("");
  const rateLimit = useRateLimitNotice();

  const clear = () => {
    said.value = "";
    rateLimit.clear();
  };

  const refuse: ActionResult["refuse"] = (error, fallback, labels) => {
    clear();
    if (refusalOfTheReader(error.status)) return;
    takeOverRefusal(error);
    if (rateLimit.take(error)) return;
    said.value = describeFailure(error, fallback, labels);
  };

  return {
    refusal: computed(() => rateLimit.message.value || said.value),
    waiting: rateLimit.waiting,
    refuse,
    clear,
  };
}
