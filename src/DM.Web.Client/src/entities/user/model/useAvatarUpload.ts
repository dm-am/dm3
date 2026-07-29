import { computed, onMounted, onUnmounted, ref, type Ref } from "vue";
import type { AxiosProgressEvent } from "axios";
import { personalApi, uploadApi } from "@/shared/api";
import { useAuthStore } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { compressImage } from "@/shared/lib/utils/imageCompression";
import type { User } from "@/shared/api/models/community/users";

/**
 * Supported formats: must match the server whitelist
 * (ImageProcessingService.ExtensionByContentType). FE and BE share one contract.
 */
export const AVATAR_ACCEPT = "image/jpeg,image/png,image/webp";
const ALLOWED_MIME = new Set(["image/jpeg", "image/png", "image/webp"]);

/**
 * SSOT for avatar upload/reset. Used by the ProfilePictureUpload overlay
 * on the profile page — the single UX entry point (previously duplicated in
 * AccountProfileSection, which was an IA anti-pattern: the avatar is a profile attr,
 * not an account setting).
 *
 * Behavior:
 * - Client-side resize to 1024×1024 (Canvas, no deps) — saves bandwidth.
 * - Drag-drop via handleDrop / clipboard paste via a document-level listener.
 * - Progress via axios onUploadProgress.
 * - Idempotency via `crypto.randomUUID()` in uploadApi (under the hood).
 * - After a successful upload/reset — userStore.fetchUser(), plus a SignalR
 *   `UserAvatarChanged` broadcast to open tabs.
 *
 * @param user — a reactive ref to the User (.id is needed for targetId and to
 *   determine "is there anything to reset").
 */
export function useAvatarUpload(user: Ref<User | null | undefined>) {
  const userStore = useAuthStore();
  const toast = useToast();

  const uploading = ref(false);
  const resetting = ref(false);
  const progress = ref(0);
  const isDragover = ref(false);

  const hasAvatar = computed(() => {
    const p = user.value?.picture;
    return !!(p?.originalUrl || p?.mediumUrl || p?.smallUrl);
  });

  const busy = computed(() => uploading.value || resetting.value);

  async function uploadFile(rawFile: File) {
    if (busy.value) return;
    if (!user.value) return;

    if (!ALLOWED_MIME.has(rawFile.type)) {
      toast.error("Допустимы только JPEG, PNG или WebP");
      return;
    }

    uploading.value = true;
    progress.value = 0;
    try {
      // Client-side compression: cut down to 1024×1024 via the Canvas API.
      // The server resizes anyway, but this saves upload bandwidth on mobile.
      const file = await compressImage(rawFile);

      const onProgress = (e: AxiosProgressEvent) => {
        if (e.total) {
          progress.value = Math.round((e.loaded / e.total) * 100);
        }
      };

      const { data: uploadData, error: uploadError } =
        await uploadApi.directUpload(file, "UserAvatar", {
          targetId: user.value.id,
          onProgress,
        });
      if (uploadError || !uploadData) {
        toast.error("Не удалось загрузить аватар");
        return;
      }

      const { error: profileError } = await personalApi.updateMyProfile({
        avatarUploadId: uploadData.id,
      });
      if (profileError) {
        toast.error("Не удалось обновить профиль");
        return;
      }

      await userStore.fetchUser();
      toast.success("Аватар успешно обновлен");
    } catch {
      toast.error("Не удалось загрузить аватар");
    } finally {
      uploading.value = false;
      progress.value = 0;
    }
  }

  /**
   * Removes the avatar. Asking first is the caller's job — confirmation is a
   * UI concern and this composable owns no template.
   */
  async function resetAvatar() {
    if (!hasAvatar.value || busy.value) return;
    resetting.value = true;
    try {
      const { error } = await personalApi.removeMyAvatar();
      if (error) {
        toast.error("Не удалось сбросить аватар");
        return;
      }
      await userStore.fetchUser();
      toast.success("Аватар сброшен");
    } catch {
      toast.error("Не удалось сбросить аватар");
    } finally {
      resetting.value = false;
    }
  }

  // Native event handlers — call sites bind via template @drop / @dragover.

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

  // Clipboard paste: a global listener on document. Fires on any
  // Ctrl+V while the page is focused. If the clipboard holds an image — upload it.
  async function handlePaste(e: ClipboardEvent) {
    if (busy.value) return;
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
    document.removeEventListener("paste", handlePaste);
  });

  return {
    // State
    uploading,
    resetting,
    progress,
    isDragover,
    hasAvatar,
    busy,
    // Actions
    uploadFile,
    resetAvatar,
    // Event handlers (for template @-bindings)
    onDragEnter,
    onDragOver,
    onDragLeave,
    onDrop,
  };
}
