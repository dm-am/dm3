import type {
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
} from "@/shared/api/models/notepads";
import { notepadEndpoints } from "@/shared/api";

/**
 * Personal notepad (`/v1/users/me/notepad`).
 *
 * Blog and game notepads are not here: they live on blogApi and gameApi, next to
 * the rest of their container's surface.
 */
export default new (class NotepadApi {
  private notepad = notepadEndpoints("users/me/notepad");

  /**
   * Get all user notepad entries
   */
  public getUserEntries() {
    return this.notepad.list();
  }

  /**
   * Get user notepad entry by ID
   */
  public getUserEntry(id: string) {
    return this.notepad.get(id);
  }

  /**
   * Create user notepad entry
   */
  public createUserEntry(request: CreateNotepadEntryRequest) {
    return this.notepad.create(request);
  }

  /**
   * Update user notepad entry
   */
  public updateUserEntry(id: string, request: UpdateNotepadEntryRequest) {
    return this.notepad.update(id, request);
  }

  /**
   * Delete user notepad entry
   */
  public deleteUserEntry(id: string) {
    return this.notepad.remove(id);
  }
})();
