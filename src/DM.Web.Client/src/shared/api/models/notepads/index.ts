// Notepad type
export enum NotepadType {
  Player = 1,
  Master = 2,
  Blog = 3,
  User = 4,
}

// Notepad entry DTO
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

// Notepad category DTO
export interface NotepadCategory {
  id: string;
  notepadType: NotepadType;
  containerId: string;
  ownerId?: string | null;
  name: string;
  sortOrder: number;
  createdUtc: string;
}

// Create notepad entry request
export interface CreateNotepadEntryRequest {
  categoryId?: string | null;
  title: string;
  content: string;
}

// Update notepad entry request
export interface UpdateNotepadEntryRequest {
  categoryId?: string | null;
  title?: string;
  content?: string;
  sortOrder?: number | null;
}

// Create notepad category request
export interface CreateNotepadCategoryRequest {
  name: string;
}

// Update notepad category request
export interface UpdateNotepadCategoryRequest {
  name?: string;
  sortOrder?: number | null;
}
