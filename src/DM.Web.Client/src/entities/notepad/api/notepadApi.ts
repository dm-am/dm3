import type {
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
} from "@/shared/api/models/notepads";
import { notepadEndpoints } from "@/shared/api";

/**
 * The refusal a caller takes over because its own screen carries it: the
 * confirmation before a delete prints the refusal in itself, and the response
 * interceptor has to stay quiet or the same sentence stands in two places.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

/** The calls of the signed-in reader's own notepad. */
const notepad = notepadEndpoints("users/me/notepad");

/**
 * Personal notepad (`/v1/users/me/notepad`).
 *
 * Blog and game notepads are not here: they live on blogApi and gameApi, next to
 * the rest of their container's surface.
 */
export default new (class NotepadApi {
  /**
   * Get all user notepad entries
   */
  public getUserEntries() {
    return notepad.list();
  }

  /**
   * Get user notepad entry by ID
   */
  public getUserEntry(id: string) {
    return notepad.get(id);
  }

  /**
   * Create user notepad entry
   */
  public createUserEntry(request: CreateNotepadEntryRequest) {
    return notepad.create(request);
  }

  /**
   * Update user notepad entry
   */
  public updateUserEntry(id: string, request: UpdateNotepadEntryRequest) {
    return notepad.update(id, request);
  }

  /**
   * Delete user notepad entry
   */
  public deleteUserEntry(id: string, options?: RefusalFlags) {
    return notepad.remove(id, options);
  }
})();
