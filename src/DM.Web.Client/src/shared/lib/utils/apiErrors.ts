import type { BadRequestError } from "@/shared/api/models/common";

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
 * Get first error message for a field, or undefined.
 */
export function getFieldError(
  errors: Record<string, string[]>,
  field: string,
): string | undefined {
  return errors[field.toLowerCase()]?.[0];
}
