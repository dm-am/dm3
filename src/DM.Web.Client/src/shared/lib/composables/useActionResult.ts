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
  /** Writes a refusal at the place and takes down the held plate; status 0 and 401 stay the plate's. */
  refuse: (
    error: GeneralError | BadRequestError,
    fallback: string,
    labels?: FieldLabels,
  ) => void;
  /** Empties the line: the place is pressed again, or it changed. */
  clear: () => void;
}

/**
 * The refusal of a press, written as a line at the place (ActionResult) until
 * the place is pressed again. A rate limit counts down there and `waiting`
 * keeps the control shut. Worded by describeFailure.
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
