import { computed, reactive, ref } from "vue";
import {
  MAX_POST_ATTACHMENTS,
  everyReader,
  pickAttachments,
  uploadPostAttachments,
  type PendingAttachment,
} from "@/features/upload";
import type { UploadAudience } from "@/shared/api/models/common/upload";
import type { usePostAttachments } from "./usePostAttachments";

/**
 * The files an edit chose: they wait for "Сохранить", so each goes with the
 * mark chosen for it and is never seen wider. Refusals and the wait are the
 * attached files' own, so the edit shows one line and one state for files.
 */
export function useEditAttachments(
  postId: () => string | undefined,
  attachments: ReturnType<typeof usePostAttachments>,
) {
  const pending = ref<PendingAttachment[]>([]);
  const full = computed(
    () =>
      attachments.list.length + pending.value.length >= MAX_POST_ATTACHMENTS,
  );

  function choose(chosen: File[]) {
    if (attachments.busy || chosen.length === 0) return;
    const picked = pickAttachments(
      chosen,
      attachments.list.length + pending.value.length,
    );
    attachments.error = picked.error;
    pending.value = [
      ...pending.value,
      ...picked.accepted.map((file) => ({ file, audience: everyReader() })),
    ];
  }

  function unchoose(index: number) {
    pending.value = pending.value.filter((_, i) => i !== index);
    attachments.error = null;
  }

  function mark(index: number, audience: UploadAudience) {
    pending.value = pending.value.map((one, i) =>
      i === index ? { ...one, audience } : one,
    );
  }

  /** An edit opened or closed: what it chose and what it was refused go with it. */
  function forget() {
    pending.value = [];
    attachments.error = null;
  }

  /** Sends the chosen files once the text is saved; the names that failed. */
  async function send(): Promise<string[]> {
    const id = postId();
    if (!id || pending.value.length === 0) return [];
    attachments.busy = true;
    const { uploadedCount, failedNames } = await uploadPostAttachments(
      id,
      pending.value,
    );
    attachments.busy = false;
    pending.value = [];
    if (uploadedCount > 0) await attachments.refresh();
    return failedNames;
  }

  return reactive({ pending, full, choose, unchoose, mark, forget, send });
}
