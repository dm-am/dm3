import type {
  ApiResult,
  Envelope,
  ListEnvelope,
} from "@/shared/api/models/common";
import type { NotepadEntry } from "@/shared/api/models/notepads";

/** What the board sends when creating or updating an entry. */
export interface NotepadEntryInput {
  title: string;
  content: string;
}

/**
 * The four calls a notepad needs, bound to one container by the page that
 * owns it. The board never learns whether it is talking to the personal,
 * game or blog endpoint — that is the whole point of the seam: the three
 * notepads were identical UIs kept in three copies, and they drifted.
 */
export interface NotepadAdapter {
  list(): Promise<ApiResult<ListEnvelope<NotepadEntry>>>;
  create(input: NotepadEntryInput): Promise<ApiResult<Envelope<NotepadEntry>>>;
  update(
    id: string,
    input: NotepadEntryInput,
  ): Promise<ApiResult<Envelope<NotepadEntry>>>;
  /**
   * The flags are the delete confirmation's, which prints a refusal in
   * itself: passed on to the request, they keep the interceptor from saying
   * it a second time. An adapter that drops them leaves the interceptor
   * speaking as well.
   */
  remove(
    id: string,
    options?: { ownsRefusal?: boolean; ownsRateLimit?: boolean },
  ): Promise<ApiResult<void>>;
}
