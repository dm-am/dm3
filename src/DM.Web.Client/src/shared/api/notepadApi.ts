import type { Envelope, ListEnvelope } from "./models/common";
import type {
  NotepadEntry,
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
} from "./models/notepads";
import Api from "./client";

/**
 * Personal notepad (`/v1/users/me/notepad`).
 *
 * Blog and game notepads are not here: they live on blogApi and gameApi, next to
 * the rest of their container's surface. Notepad categories are not here either
 * — the server exposes no route for them.
 */
export default new (class NotepadApi {
  private userPath = "users/me/notepad";

  /**
   * Get all user notepad entries
   */
  public getUserEntries() {
    return Api.get<ListEnvelope<NotepadEntry>>(this.userPath);
  }

  /**
   * Get user notepad entry by ID
   */
  public getUserEntry(id: string) {
    return Api.get<Envelope<NotepadEntry>>(`${this.userPath}/${id}`);
  }

  /**
   * Create user notepad entry
   */
  public createUserEntry(request: CreateNotepadEntryRequest) {
    return Api.post<Envelope<NotepadEntry>>(this.userPath, request);
  }

  /**
   * Update user notepad entry
   */
  public updateUserEntry(id: string, request: UpdateNotepadEntryRequest) {
    return Api.patch<Envelope<NotepadEntry>>(`${this.userPath}/${id}`, request);
  }

  /**
   * Delete user notepad entry
   */
  public deleteUserEntry(id: string) {
    return Api.delete(`${this.userPath}/${id}`);
  }
})();
