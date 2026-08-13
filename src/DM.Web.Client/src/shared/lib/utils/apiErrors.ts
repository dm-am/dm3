import type { BadRequestError } from "@/shared/api/models/common";
import { readFieldError } from "@/shared/lib/errors/validationErrors";

/**
 * Parse API validation errors into a normalized format.
 * Reads the errors dictionary of a validation problem document.
 * Keys are lowercased for consistent access.
 */
export function parseApiErrors(
  error: BadRequestError | null | undefined,
): Record<string, string[]> {
  if (!error) return {};

  const raw = error.errors ?? {};
  const result: Record<string, string[]> = {};

  for (const [key, value] of Object.entries(raw)) {
    result[key.toLowerCase()] = value;
  }

  return result;
}

/**
 * The first thing wrong with a field, ready to show under it.
 *
 * This used to return the entry as it stands while a second function with the
 * same job, one module over, translated the code into a sentence. The domain
 * answers in codes — "Short", "Long", "Invalid" — so the difference reached the
 * reader: one form showed a message under the field and the next showed an
 * English word.
 *
 * An unknown code is returned as it stands, so a new code on the server is
 * visible rather than hidden behind a generic phrase.
 */
export function getFieldError(
  errors: Record<string, string[]>,
  field: string,
): string | undefined {
  return readFieldError(errors, field);
}
