import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure } from "./describeFailure";

/**
 * The statuses the response interceptor already announced.
 *
 * A failure gets one toast, from whichever layer knows most about it. For these
 * the interceptor knows more than the call site does: an expired session is
 * about the session and not about the request, a rate limit carries the
 * retry-after value, and a 5xx or a dead connection means the server said
 * nothing a caller could relay. So the interceptor speaks and the call site
 * stays quiet.
 *
 * Everything else — a rejected form, a missing entity, a conflict — belongs to
 * the call site: only it knows which action was refused, and the server sent a
 * problem document worth reading out.
 */
function announcedByInterceptor(status: number): boolean {
  return status === 401 || status === 403 || status === 429 || status >= 500;
}

/**
 * Show the one toast a failed request deserves.
 *
 * Before this, a failure often produced two: the interceptor's generic sentence
 * plus the call site's specific one. Both are errors, error toasts do not
 * auto-dismiss, and the deduplication in useToast is by message text — so two
 * different sentences about one failure sat on screen until dismissed by hand.
 *
 * @param error The problem document from the failed request
 * @param fallback What to say when the server named nothing
 */
export function notifyFailure(
  error: GeneralError | BadRequestError,
  fallback: string,
): void {
  if (announcedByInterceptor(error.status)) {
    return;
  }

  useToast().error(describeFailure(error, fallback));
}
