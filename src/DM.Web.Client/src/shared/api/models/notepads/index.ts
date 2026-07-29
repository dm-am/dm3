/**
 * Notepad contract (`/v1/users/me/notepad`, `/v1/games/{id}/notepad`,
 * `/v1/blogs/{id}/notepad`) — one shape for all three, because the server
 * serves all three from one DTO. The game and blog entities used to declare
 * their own copies and they drifted; this module is the only declaration.
 */

/**
 * Which notepad an entry belongs to. Serialised by name — the API registers a
 * string enum converter globally, so the wire carries "Master", not 2.
 */
export type NotepadType = "Player" | "Master" | "Blog" | "User";

export interface NotepadEntry {
  id: string;
  notepadType: NotepadType;
  containerId: string;
  ownerId?: string | null;
  categoryId?: string | null;
  title: string;
  content: string;
  sortOrder: number;
  createdUtc: string;
  modifiedUtc?: string | null;
}

export interface CreateNotepadEntryRequest {
  categoryId?: string | null;
  title: string;
  content: string;
}

/**
 * A PATCH, but title and content are not optional: the server binds them into
 * non-nullable properties and rejects the request when either is missing.
 */
export interface UpdateNotepadEntryRequest {
  categoryId?: string | null;
  title: string;
  content: string;
  sortOrder?: number | null;
}
