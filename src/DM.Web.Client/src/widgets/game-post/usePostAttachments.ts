import { computed, reactive, ref } from "vue";
import type { PostAttachment } from "@/entities/game";
import { postApi } from "@/entities/game";
import { uploadApi } from "@/shared/api";
import type { UploadAudience } from "@/shared/api/models/common/upload";
import { refusalHere } from "@/shared/lib/errors";

/**
 * The files attached to one post and the requests that change them, kept
 * locally once shown. The mark and the removal act at once, as the cross always
 * has; a file chosen in the edit is useEditAttachments'.
 */
export function usePostAttachments(
  postId: () => string | undefined,
  initial: () => PostAttachment[] | undefined,
) {
  const override = ref<PostAttachment[] | null>(null);
  const list = computed<PostAttachment[]>(
    () => override.value ?? initial() ?? [],
  );
  const error = ref<string | null>(null);
  const busy = ref(false);

  async function mark(attachment: PostAttachment, audience: UploadAudience) {
    if (busy.value) return;
    busy.value = true;
    error.value = null;
    const { data, error: refused } = await uploadApi.updateAudience(
      attachment.id,
      audience,
    );
    busy.value = false;
    if (refused) {
      // The server's words: "виден всем, но вставлен только внутрь скрытого блока" among them.
      error.value = refusalHere(refused, "Не удалось изменить, кто видит файл");
      return;
    }
    const upload = data?.resource;
    if (!upload) {
      await refresh();
      return;
    }
    override.value = list.value.map((one) =>
      one.id === attachment.id
        ? {
            ...one,
            recipients: upload.recipients,
            addressees: upload.addressees,
          }
        : one,
    );
  }

  async function remove(attachment: PostAttachment) {
    if (busy.value) return;
    busy.value = true;
    error.value = null;
    const { error: refused } = await uploadApi.deleteUpload(attachment.id);
    busy.value = false;
    if (refused) {
      // Under the attachments of the edit, with the other refusals about files.
      error.value = refusalHere(refused, "Не удалось убрать вложение");
      return;
    }
    override.value = list.value.filter((a) => a.id !== attachment.id);
  }

  /** Re-reads the post: the server, not the client, decides the order of its files. */
  async function refresh() {
    const id = postId();
    if (!id) return;
    const { data } = await postApi.getPost(id);
    if (data?.resource) {
      override.value = data.resource.attachments ?? [];
    }
  }

  return reactive({ list, error, busy, mark, remove, refresh });
}
