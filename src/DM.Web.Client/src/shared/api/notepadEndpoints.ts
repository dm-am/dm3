import Api from "./client";
import type { Envelope, ListEnvelope } from "./models/common";
import type {
  NotepadEntry,
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
} from "./models/notepads";

/** The refusal flags of a confirmation that prints a refused delete itself. */
type RemoveOptions = Parameters<typeof Api.delete>[2];

/**
 * The five notepads, which the server serves from one DTO and one controller
 * shape. The method that names each notepad stays with its container's client.
 *
 * @param base The path of the notepad itself, without a trailing slash.
 */
export function notepadEndpoints(base: string) {
  // Composed here, not in each call: routes.spec.ts would read a template
  // literal inside the call as the address "{param}/{param}".
  const entry = (entryId: string) => `${base}/${entryId}`;

  return {
    list: () => Api.get<ListEnvelope<NotepadEntry>>(base),
    get: (entryId: string) => Api.get<Envelope<NotepadEntry>>(entry(entryId)),
    create: (input: CreateNotepadEntryRequest) =>
      Api.post<Envelope<NotepadEntry>>(base, input),
    update: (entryId: string, input: UpdateNotepadEntryRequest) =>
      Api.patch<Envelope<NotepadEntry>>(entry(entryId), input),
    remove: (entryId: string, options?: RemoveOptions) =>
      Api.delete(entry(entryId), undefined, options),
  };
}
