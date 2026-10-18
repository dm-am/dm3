import { ref } from "vue";
import uploadApi from "@/shared/api/uploadApi";
import type { Upload } from "@/shared/api/models/common/upload";

/**
 * Deleting an uploaded file, from the row the button was pressed on to the
 * reload after the server agreed: which row is being asked about, and the
 * request the confirmation runs.
 *
 * Both upload screens (the owner's and the moderation-wide one) carried this
 * verbatim. The endpoint is one — DELETE /v1/uploads/{id}, owner-or-admin
 * server-side — so there was nothing for the two copies to disagree on, and
 * the last difference between them (the client each called it through) went
 * away when the moderation page stopped routing the delete through its own.
 *
 * The wait, the refusal and the closing are the confirmation window's own
 * (ConfirmDialog, `action`): it stays open until the answer, prints a refusal
 * in itself and closes on success alone. So the request carries the flags
 * that keep the response interceptor quiet, and this holds no busy flag and
 * says nothing about a failure.
 *
 * @param reload Re-reads the current page once the file is gone.
 */
export function useUploadDelete(reload: () => Promise<void> | void) {
  /** The row the confirmation is about; null when nothing is being deleted. */
  const deleteTarget = ref<Upload | null>(null);

  async function deleteFile() {
    const target = deleteTarget.value;
    if (!target) return;
    const result = await uploadApi.deleteUpload(target.id, {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    // Success says nothing: the window closes and the reloaded table comes back
    // without the row the button was pressed on, both on the current screen.
    if (!result.error) void reload();
    return result;
  }

  return { deleteTarget, deleteFile };
}
