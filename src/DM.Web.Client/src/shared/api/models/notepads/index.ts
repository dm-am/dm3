/**
 * Notepad contract (`/v1/users/me/notepad`, `/v1/games/{id}/notepad`,
 * `/v1/blogs/{id}/notepad`) — one shape for all three, because the server
 * serves all three from one DTO. The game and blog entities used to declare
 * their own copies and they drifted; this module is the only declaration.
 */

/**
 * Which notepad an entry belongs to. Serialised by name — the API registers a
 * string enum converter globally, so the wire carries "Master", not 2.
 *
 * Three of the five hang off a game and the on-screen names do not repeat the
 * wire ones: "Master" is the notepad of the game itself ("Заметки игры"),
 * "Player" a character's own notes kept by its owner ("Заметки игрока"), and
 * "CharacterMaster" the notes the game leads keep about that same character
 * ("Заметки мастера").
 */
export type NotepadType =
  | "Player"
  | "Master"
  | "CharacterMaster"
  | "Blog"
  | "User";

export interface NotepadEntry {
  id: string;
  notepadType: NotepadType;
  containerId: string;
  ownerId?: string | null;
  /** Who wrote the entry: the only one the API lets edit it. */
  authorId: string;
  title: string;
  content: string;
  sortOrder: number;
  createdUtc: string;
  modifiedUtc?: string | null;
}

export interface CreateNotepadEntryRequest {
  title: string;
  content: string;
}

/** A PATCH: omitted fields are left unchanged.*/
export interface UpdateNotepadEntryRequest {
  title?: string;
  content?: string;
  sortOrder?: number | null;
}
