import type { BadRequestError } from "@/shared/api/models/common";

/** The errors dictionary of a validation problem document, keys lowercased. */
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
