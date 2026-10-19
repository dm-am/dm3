import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { describeRateLimit } from "./describeRateLimit";
import { readFieldErrors, type FieldLabels } from "./validationErrors";

/**
 * The most specific sentence for a failed request: the validation codes, the
 * title of the problem document, a 413 (refused by the edge with no document),
 * then the caller's fallback.
 *
 * @param labels The screen's labels of the fields the request sent, by the
 * name the server gives them, so a refused field is named in the sentence
 * ("Система: обязательное поле") and not left for the reader to find
 */
export function describeFailure(
  error: GeneralError | BadRequestError,
  fallback: string,
  labels?: FieldLabels,
): string {
  const fields = (error as BadRequestError).errors;

  if (fields) {
    const readable = readFieldErrors(fields, labels);
    if (readable) return readable;
  }

  if (error.title) return error.title;

  // No number: the limit lives in the edge configuration.
  if (error.status === 413) return "Файл слишком большой";

  return fallback;
}

/**
 * describeFailure, except that a wait keeps the shared wording, which carries
 * the number the reader acts on.
 */
export function describeFailureOrWait(
  error: GeneralError,
  fallback: string,
): string {
  return error.status === 429
    ? describeRateLimit(error.retryAfterSeconds)
    : describeFailure(error, fallback);
}
