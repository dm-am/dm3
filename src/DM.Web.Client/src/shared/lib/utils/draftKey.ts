/**
 * The key a composer saves its draft under: what kind of subject, which one,
 * and what is written. Closed unions and server ids without the separator, so
 * two composers spell the same key only by being the same composer.
 */

/** A subject there are many of, told apart by its identifier. */
export type DraftEntity =
  "topic" | "blog" | "publication" | "game" | "room" | "chat" | "ticket";

/**
 * Subjects there is one of: the site-wide chat and the two ticket forms. A
 * value, since the builder has to know it at runtime; the union derives from it.
 */
const PLACES = { "global-chat": true, support: true, complaint: true } as const;

export type DraftPlace = keyof typeof PLACES;

/** What is being written. One composer per purpose within a subject. */
export type DraftPurpose =
  | "post"
  | "metagame"
  | "comment"
  | "message"
  | "publication"
  | "ticket"
  | "review";

/** Stands where the identifier would be for a subject that has none. */
const NO_ENTITY = "-";

export function composerDraftKey(
  subject: DraftPlace,
  purpose: DraftPurpose,
): string;
export function composerDraftKey(
  subject: DraftEntity,
  purpose: DraftPurpose,
  id: string | null | undefined,
): string;
export function composerDraftKey(
  subject: DraftEntity | DraftPlace,
  purpose: DraftPurpose,
  id?: string | null,
): string {
  if (subject in PLACES) return `${subject}:${NO_ENTITY}:${purpose}`;
  // The subject has not loaded yet, so there is no key at all: a placeholder
  // one would be a bucket every unloaded composer of that kind writes into.
  if (!id) return "";
  return `${subject}:${id}:${purpose}`;
}
