import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { readValidationCode } from "./validationErrors";

/**
 * The best sentence available for a failed request.
 *
 * There are four sources, in descending order of how much they tell the
 * reader, and the point of this function is that the most specific one wins:
 *
 * 1. per-field validation codes, which say what to correct;
 * 2. the problem document's title, which the API fills for anything it names;
 * 3. a 413, which arrives with no document at all — the edge refuses a body over
 *    its limit and the API never sees the request, so the status is the message;
 * 4. the caller's fallback, for a request that never reached the server.
 *
 * Before this, several forms threw their fallback on any error at all, so a
 * wrong current password and a new password that broke the policy produced the
 * same sentence — and those call for opposite corrections. The server had said
 * which was which the whole time.
 */
export function describeFailure(
  error: GeneralError | BadRequestError,
  fallback: string,
): string {
  const fields = (error as BadRequestError).errors;

  if (fields) {
    const readable = Object.values(fields)
      .map((codes) => codes?.[0])
      .filter((code): code is string => !!code)
      .map(readValidationCode);

    // Deduplicated: two fields failing the same rule is one thing to fix, and
    // repeating the sentence reads like two separate problems.
    const unique = [...new Set(readable)];
    if (unique.length) return unique.join(". ");
  }

  if (error.title) return error.title;

  // No number in the sentence: the limit lives in the edge configuration, and a
  // copy of it here drifts from the one actually being enforced.
  if (error.status === 413) return "Файл слишком большой";

  return fallback;
}
