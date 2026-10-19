/** Reads "Envelope { resource } or bare payload" of a single-resource response. */
export function unwrapResource<T>(payload: unknown): T | null {
  if (!payload || typeof payload !== "object") return null;
  if ("resource" in payload) return (payload as { resource: T }).resource;
  return payload as T;
}
