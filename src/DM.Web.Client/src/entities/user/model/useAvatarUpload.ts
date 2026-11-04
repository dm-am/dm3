import { onMounted, onUnmounted, ref } from "vue";
import type { AxiosProgressEvent } from "axios";
import { uploadApi } from "@/shared/api";
import { compressImage } from "@/shared/lib/utils/imageCompression";
import { refusalHere } from "@/shared/lib/errors";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

// The server's whitelist, ImageProcessingService.ExtensionByContentType.
export const AVATAR_ACCEPT = "image/jpeg,image/png,image/webp";
const ALLOWED_MIME = new Set(AVATAR_ACCEPT.split(","));

/** A picture that went up and waits for "Сохранить", which links it to the profile. */
export interface UploadedAvatar {
  uploadId: string;
  /** The file as it went up, drawn in place of the saved picture meanwhile. */
  file: File;
}

/**
 * Sends a picked, dropped or pasted picture up for the owner's edit mode and
 * hands it over; the profile does not change until the edit is saved. No
 * toasts: a refused upload goes into `refusal`, under the picture.
 */
export function useAvatarUpload(
  userId: () => string | undefined,
  onUploaded: (avatar: UploadedAvatar) => void,
) {
  const uploading = ref(false);
  const progress = ref(0);
  const isDragover = ref(false);

  // A line under the picture, where the reader looks, not a toast that costs a click.
  const refusal = ref<string | null>(null);

  // An upload that lands after the overlay is gone has nobody to show it to.
  let disposed = false;

  async function uploadFile(rawFile: File) {
    if (uploading.value) return;
    const targetId = userId();
    if (!targetId) return;

    refusal.value = null;

    if (!ALLOWED_MIME.has(rawFile.type)) {
      // A drop and a paste never pass the accept filter of the picker.
      refusal.value = "Для аватара допустимы только JPEG, PNG или WebP";
      return;
    }

    uploading.value = true;
    progress.value = 0;
    try {
      // The server resizes anyway; this saves the upload on a phone.
      const file = await compressImage(rawFile);

      const onProgress = (e: AxiosProgressEvent) => {
        if (e.total) {
          progress.value = Math.round((e.loaded / e.total) * 100);
        }
      };

      const { data, error } = await uploadApi.directUpload(file, "UserAvatar", {
        targetId,
        onProgress,
      });
      if (error) {
        refusal.value = refusalHere(error, "Не удалось загрузить аватар");
        return;
      }
      // A successful upload is a 201 carrying the record it created.
      const uploadId = data!.id;
      if (disposed) {
        // Linked to nothing, it would wait in the owner's uploads for nobody.
        void uploadApi.deleteUpload(uploadId, OWNS_REFUSAL);
        return;
      }
      onUploaded({ uploadId, file });
    } catch {
      // A picture the browser cannot decode or shrink.
      refusal.value = "Не удалось загрузить аватар";
    } finally {
      uploading.value = false;
      progress.value = 0;
    }
  }

  function onDragEnter(e: DragEvent) {
    e.preventDefault();
    isDragover.value = true;
  }

  function onDragOver(e: DragEvent) {
    e.preventDefault();
  }

  function onDragLeave(e: DragEvent) {
    e.preventDefault();
    isDragover.value = false;
  }

  async function onDrop(e: DragEvent) {
    e.preventDefault();
    isDragover.value = false;
    const file = e.dataTransfer?.files?.[0];
    if (file) await uploadFile(file);
  }

  // On the document, so any paste of an image while the page is focused uploads it.
  async function handlePaste(e: ClipboardEvent) {
    if (uploading.value) return;
    const items = e.clipboardData?.items;
    if (!items) return;
    for (const item of items) {
      if (item.type.startsWith("image/")) {
        const file = item.getAsFile();
        if (file) {
          await uploadFile(file);
          return;
        }
      }
    }
  }

  onMounted(() => {
    document.addEventListener("paste", handlePaste);
  });
  onUnmounted(() => {
    disposed = true;
    document.removeEventListener("paste", handlePaste);
  });

  return {
    uploading,
    progress,
    isDragover,
    refusal,
    uploadFile,
    onDragEnter,
    onDragOver,
    onDragLeave,
    onDrop,
  };
}
