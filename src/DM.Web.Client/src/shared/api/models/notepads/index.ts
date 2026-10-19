/**
 * Notepad contract (/v1/users/me/notepad, /v1/games/{id}/notepad,
 * /v1/blogs/{id}/notepad, the two of a character): one shape for all five,
 * served from one DTO.
 */

/**
 * Which notepad an entry belongs to, by name on the wire. "Master" is the
 * game's own ("Заметки игры"), "Player" a character's owner's ("Заметки
 * игрока"), "CharacterMaster" the game leads' about that character ("Заметки
 * мастера").
 */
type NotepadType = "Player" | "Master" | "CharacterMaster" | "Blog" | "User";

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
