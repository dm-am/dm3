import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure } from "./describeFailure";
import { nameGeneral } from "./generalPlate";
import type { FieldLabels } from "./validationErrors";

/**
 * The statuses the response interceptor already announced.
 *
 * A failure gets one toast, from whichever layer knows most about it. For these
 * the interceptor knows more than the call site does: an expired session is
 * about the session and not about the request, a refused action is answered
 * with the reason the server named and the interceptor relays that reason
 * verbatim, a rate limit carries the retry-after value, and a 5xx or a dead
 * connection means the server said nothing a caller could relay. So the
 * interceptor speaks and the call site stays quiet.
 *
 * Everything else — a rejected form, a missing entity, a conflict — belongs to
 * the call site: only it knows which action was refused, and the server sent a
 * problem document worth reading out.
 *
 * Exported because the rule is about who speaks, not about toasts: a form shows
 * the same failures in itself, under the field, and has to keep quiet about the
 * ones already on screen. A request can also take a status back — `Api.post`
 * with `ownsRefusal` silences the 403 toast, and `ownsRateLimit` the 429 one —
 * and then the caller says so where it asks, before it consults this.
 */
export function announcedByInterceptor(status: number): boolean {
  // Zero is the request that never reached the server: axios answers a dead
  // connection and a timed-out one with no response at all, and the interceptor
  // always speaks for that. Missing from this list, it let a form put its own
  // fallback under the field beside the toast - one event said twice, in two
  // different sentences, and only one of them offering the other address.
  return (
    status === 0 ||
    status === 401 ||
    status === 403 ||
    status === 429 ||
    status >= 500
  );
}

/**
 * Show the one toast a failed request deserves.
 *
 * Before this, a failure often produced two: the interceptor's generic sentence
 * plus the call site's specific one. Both are errors, error toasts do not
 * auto-dismiss, and the deduplication in useToast is by message text — so two
 * different sentences about one failure sat on screen until dismissed by hand.
 *
 * The fallback does one more job on the statuses the interceptor speaks for.
 * Where the server named nothing beyond "no rights" or "server error", the
 * interceptor holds its plate for the call to name, and the fallback is that
 * name: "Не удалось сохранить пост: ошибка сервера" (generalPlate.ts). Still one
 * plate, and now it says which press it answers.
 *
 * @param error The problem document from the failed request
 * @param fallback What to say when the server named nothing
 * @param labels The screen's labels of the fields the request sent, by the
 * server's name of each, so a refused field is named on the plate
 */
export function notifyFailure(
  error: GeneralError | BadRequestError,
  fallback: string,
  labels?: FieldLabels,
): void {
  if (announcedByInterceptor(error.status)) {
    nameGeneral(error, fallback);
    return;
  }

  useToast().error(describeFailure(error, fallback, labels));
}
