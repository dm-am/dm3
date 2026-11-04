import { ref } from "vue";
import uploadApi from "@/shared/api/uploadApi";
import type { Upload } from "@/shared/api/models/common/upload";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

/**
 * Deleting an uploaded file (DELETE /v1/uploads/{id}) from the row it was
 * pressed on, for both upload screens. The confirmation window owns the wait,
 * the refusal and the closing, so the request carries the flags that keep the
 * interceptor quiet.
 *
 * @param reload Re-reads the current page once the file is gone.
 */
export function useUploadDelete(reload: () => Promise<void> | void) {
  /** The row the confirmation is about; null when nothing is being deleted. */
  const deleteTarget = ref<Upload | null>(null);

  async function deleteFile() {
    const target = deleteTarget.value;
    if (!target) return;
    const result = await uploadApi.deleteUpload(target.id, OWNS_REFUSAL);
    // Success says nothing: the window closes and the reloaded table comes back
    // without the row the button was pressed on, both on the current screen.
    if (!result.error) void reload();
    return result;
  }

  return { deleteTarget, deleteFile };
}
