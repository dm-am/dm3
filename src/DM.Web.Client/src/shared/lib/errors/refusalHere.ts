import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { describeFailure } from "./describeFailure";
import { describeRateLimit } from "./describeRateLimit";
import { takeOverRefusal } from "./generalPlate";
import type { FieldLabels } from "./validationErrors";

/**
 * An expired session and a request that never arrived belong to the plate:
 * neither is about the press.
 */
export function refusalOfTheReader(status: number): boolean {
  return status === 401 || status === 0;
}

/**
 * The sentence a page writes at the place of the press, with the held plate
 * taken down; null where the plate is the carrier (refusalOfTheReader). A
 * plate only where the page has nowhere to say it. A caller that counts the
 * wait down takes the refusal through useActionResult.
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
