import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { describeFailure } from "./describeFailure";
import { describeRateLimit } from "./describeRateLimit";
import { takeOverRefusal } from "./generalPlate";
import type { FieldLabels } from "./validationErrors";

/**
 * Whether a refusal belongs to the plate whatever the page can say: an
 * expired session and a request that never arrived.
 *
 * Neither is about the press. A 401 ends the session, and the interceptor
 * says so and sends the reader to sign in again; a request with no answer at
 * all (status 0) is about the reader's network, and the interceptor offers the
 * other address of the site once it is proven to answer. A line under the
 * button would say less than the plate and send the reader to press again.
 */
export function refusalOfTheReader(status: number): boolean {
  return status === 401 || status === 0;
}

/**
 * The sentence a page writes at the place of the press, with the plate the
 * interceptor held for the same refusal taken down. Null where the plate is
 * the right carrier (refusalOfTheReader) and the place says nothing.
 *
 * This is the owner's rule of 26 September 2026 for refusals: a plate only
 * where the page has nowhere to say it. Before it a press that had a place on
 * the screen - the button of a form, the row of a list - still answered a 403,
 * a 429 or a 5xx with a plate in the corner, and the line under the button
 * stayed empty so as not to say it twice. Now the line says it, and the plate
 * stays down.
 *
 * A rate limit is said with the wait the server named. A caller that shuts
 * its button for the wait and counts it down takes the refusal through
 * useActionResult instead, which keeps the count.
 *
 * @param error The problem document from the failed request
 * @param fallback What to say when the server named nothing
 * @param labels The screen's labels of the fields the request sent, by the
 * server's name of each, so a refused field is named in the line
 */
export function refusalHere(
  error: GeneralError | BadRequestError,
  fallback: string,
  labels?: FieldLabels,
): string | null {
  if (refusalOfTheReader(error.status)) return null;
  takeOverRefusal(error);
  if (error.status === 429) return describeRateLimit(error.retryAfterSeconds);
  return describeFailure(error, fallback, labels);
}
