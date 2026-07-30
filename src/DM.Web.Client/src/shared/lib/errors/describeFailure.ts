import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { readValidationCode } from "./validationErrors";

/**
 * The best sentence available for a failed request.
 *
 * There are three sources, in descending order of how much they tell the
 * reader, and the point of this function is that the most specific one wins:
 *
 * 1. per-field validation codes, which say what to correct;
 * 2. the problem document's title, which the API fills for anything it names;
 * 3. the caller's fallback, for a request that never reached the server.
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

  return error.title || fallback;
}
