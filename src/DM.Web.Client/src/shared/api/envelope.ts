/**
 * Envelope unwrap helper — SSOT for the defensive "Envelope { resource } or
 * bare payload" read that API consumers perform on single-resource responses.
 * Belongs next to the Envelope type (models/common); previously copy-pasted
 * per consumer.
 */
export function unwrapResource<T>(payload: unknown): T | null {
  if (!payload || typeof payload !== "object") return null;
  if ("resource" in payload) return (payload as { resource: T }).resource;
  return payload as T;
}
