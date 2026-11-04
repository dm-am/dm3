import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure } from "./describeFailure";
import { nameGeneral } from "./generalPlate";
import type { FieldLabels } from "./validationErrors";

/**
 * The statuses the response interceptor already announced: it knows more of
 * them than the call site, and a failure gets one toast. A form under its field
 * keeps quiet about these too; a request may take a status back with
 * ownsRefusal or ownsRateLimit.
 */
function announcedByInterceptor(status: number): boolean {
  // Zero is the request that never reached the server (no response at all).
  return (
    status === 0 ||
    status === 401 ||
    status === 403 ||
    status === 429 ||
    status >= 500
  );
}

/**
 * Shows the one toast a failed request deserves. On the general refusals the
 * fallback names the plate the interceptor holds (generalPlate.ts).
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
