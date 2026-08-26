import Api from "./client";
import type { Envelope, ListEnvelope } from "./models/common";
import type {
  NotepadEntry,
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
} from "./models/notepads";

/**
 * The five notepads of the site, which are one notepad five times.
 *
 * The server serves all of them from one DTO and one controller shape — a list
 * and a create under `{base}`, a single entry, a PATCH and a delete under
 * `{base}/{entryId}` — and the clients had written that out five times: the
 * reader's own, the blog's, the game's and a character's two. They differed in
 * the path and in nothing else.
 *
 * What stays with each client is the method that names the notepad and knows
 * how to build its path. Those keep living next to the rest of their
 * container's surface, because a reader looking for what a blog can do reads
 * blogApi, and a notepad client holding four of the five would not be one.
 *
 * @param base The path of the notepad itself, without a trailing slash.
 */
export function notepadEndpoints(base: string) {
  // The address of one entry is composed here rather than spelled at each call
  // below, and that is also what keeps routes.spec.ts honest: a template
  // literal inside the call would read to it as the address `{param}/{param}`,
  // which no controller serves. The gate reads the base out of the five
  // callers instead — see the notepad clause there.
  const entry = (entryId: string) => `${base}/${entryId}`;

  return {
    list: () => Api.get<ListEnvelope<NotepadEntry>>(base),
    get: (entryId: string) => Api.get<Envelope<NotepadEntry>>(entry(entryId)),
    create: (input: CreateNotepadEntryRequest) =>
      Api.post<Envelope<NotepadEntry>>(base, input),
    update: (entryId: string, input: UpdateNotepadEntryRequest) =>
      Api.patch<Envelope<NotepadEntry>>(entry(entryId), input),
    remove: (entryId: string) => Api.delete(entry(entryId)),
  };
}
