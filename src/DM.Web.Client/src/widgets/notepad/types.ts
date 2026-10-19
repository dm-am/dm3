import type {
  ApiResult,
  Envelope,
  ListEnvelope,
} from "@/shared/api/models/common";
import type { NotepadEntry } from "@/shared/api/models/notepads";
import type { RequestOptions } from "@/shared/api/interceptor";

/** What the board sends when creating or updating an entry. */
interface NotepadEntryInput {
  title: string;
  content: string;
}

/** The four calls of a notepad, bound to one container by the page that owns it. */
export interface NotepadAdapter {
  list(): Promise<ApiResult<ListEnvelope<NotepadEntry>>>;
  create(input: NotepadEntryInput): Promise<ApiResult<Envelope<NotepadEntry>>>;
  update(
    id: string,
    input: NotepadEntryInput,
  ): Promise<ApiResult<Envelope<NotepadEntry>>>;
  /** The flags keep the interceptor from repeating the confirmation's refusal. */
  remove(id: string, options?: RequestOptions): Promise<ApiResult<void>>;
}
