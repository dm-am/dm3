/**
 * The key a composer saves its draft under.
 *
 * Every composer on the site autosaves what is typed into it (BBCodeEditor,
 * `draftKey`), and the key is what makes the saved text belong to one thing
 * rather than to the screen it was typed on. The keys used to be spelled where
 * they were needed — `topic_${id}`, `blog_${id}_comment`, `post_meta_${id}`,
 * `chat_room_${id}`, `${kind}_request`, `global-chat` — six naming schemes with
 * nothing holding them apart, and the next screen was one literal away from
 * sharing a bucket with an existing one. A shared bucket is one game's comment
 * box opening with what was written for another game.
 *
 * A key is a triple: what kind of subject the composer belongs to, which
 * subject of that kind, and what is being written. Subject and purpose are
 * closed unions, an identifier is a server one (a guid), and none of the three
 * contains the separator, so two composers can only spell the same key by
 * being the same composer.
 */

/** A subject there are many of, told apart by its identifier. */
export type DraftEntity = "topic" | "blog" | "game" | "room" | "chat";

/**
 * Subjects there is exactly one of, so there is nothing to tell apart: the
 * site-wide chat and the two ticket forms. A value rather than a type, because
 * the builder has to know at runtime that these carry no identifier; the union
 * is derived from it, so the list and the type cannot drift apart.
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
  | "ticket";

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
