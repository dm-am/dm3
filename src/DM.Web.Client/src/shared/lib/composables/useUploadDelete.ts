import { ref } from "vue";
import uploadApi from "@/shared/api/uploadApi";
import type { Upload } from "@/shared/api/models/common/upload";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

/**
 * Deleting an uploaded file, from the row the button was pressed on to the
 * reload after the server agreed: which row is being asked about, whether the
 * request is in flight, and the confirm handler itself.
 *
 * Both upload screens (the owner's and the moderation-wide one) carried this
 * verbatim. The endpoint is one — DELETE /v1/uploads/{id}, owner-or-admin
 * server-side — so there was nothing for the two copies to disagree on, and
 * the last difference between them (the client each called it through) went
 * away when the moderation page stopped routing the delete through its own.
 *
 * @param reload Re-reads the current page once the file is gone.
 */
export function useUploadDelete(reload: () => Promise<void> | void) {
  const toast = useToast();

  /** The row the confirmation is about; null when nothing is being deleted. */
  const deleteTarget = ref<Upload | null>(null);
  const deleting = ref(false);

  async function confirmDelete() {
    if (!deleteTarget.value || deleting.value) return;
    deleting.value = true;
    const { error } = await uploadApi.deleteUpload(deleteTarget.value.id);
    deleting.value = false;
    if (error) {
      notifyFailure(error, "Не удалось удалить файл");
      return;
    }
    toast.success("Файл удален");
    deleteTarget.value = null;
    await reload();
  }

  return { deleteTarget, deleting, confirmDelete };
}
