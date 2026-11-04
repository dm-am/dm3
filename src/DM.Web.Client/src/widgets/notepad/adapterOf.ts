import type { NotepadAdapter } from "./types";

/** The adapter of a notepad whose address follows a route: it asks for the notepad on every call. */
export function adapterOf(notepad: () => NotepadAdapter): NotepadAdapter {
  const adapter: NotepadAdapter = {
    list: () => notepad().list(),
    create: (input) => notepad().create(input),
    update: (id, input) => notepad().update(id, input),
    remove: (id, options) => notepad().remove(id, options),
  };
  return adapter;
}
