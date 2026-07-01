import { computed, onMounted, onUnmounted, ref, type Ref } from "vue";
import type { AxiosProgressEvent } from "axios";
import { PersonalApi, UploadApi } from "@/shared/api";
import { useUserStore } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { compressImage } from "@/shared/lib/utils/imageCompression";
import type { User } from "@/shared/api/models/community/users";

/**
 * Поддерживаемые форматы: должны матчиться с whitelist'ом сервера
 * (ImageProcessingService.ExtensionByContentType). FE и BE — один контракт.
 */
export const AVATAR_ACCEPT = "image/jpeg,image/png,image/webp";
const ALLOWED_MIME = new Set(["image/jpeg", "image/png", "image/webp"]);

/**
 * SSOT для загрузки/сброса аватара. Используется ProfilePicture overlay
 * на странице профиля — единственная точка UX (раньше дублировалось в
 * AccountProfileSection, что было IA-антипаттерном: avatar — profile attr,
 * не account setting).
 *
 * Поведение:
 * - Client-side resize до 1024×1024 (Canvas, без deps) — экономит трафик.
 * - Drag-drop через handleDrop / clipboard paste через document-level listener.
 * - Прогресс через axios onUploadProgress.
 * - Идемпотентность через `crypto.randomUUID()` в UploadApi (под капотом).
 * - После успешного upload/reset — userStore.fetchUser(), плюс SignalR
 *   `UserAvatarChanged` broadcast в открытые вкладки.
 *
 * @param user — реактивный ref на User (нужен .id для targetId и для
 *   определения «есть ли что сбрасывать»).
 */
export function useAvatarUpload(user: Ref<User | null | undefined>) {
  const userStore = useUserStore();
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
      // Client-side compression: режем до 1024×1024 через Canvas API.
      // Server все равно ресайзит, но это экономит upload-трафик на mobile.
      const file = await compressImage(rawFile);

      const onProgress = (e: AxiosProgressEvent) => {
        if (e.total) {
          progress.value = Math.round((e.loaded / e.total) * 100);
        }
      };

      const { data: uploadData, error: uploadError } =
        await UploadApi.directUpload(file, "UserAvatar", {
          targetId: user.value.id,
          onProgress,
        });
      if (uploadError || !uploadData) {
        toast.error("Не удалось загрузить аватар");
        return;
      }

      const { error: profileError } = await PersonalApi.updateMyProfile({
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

  async function resetAvatar() {
    if (!hasAvatar.value || busy.value) return;
    if (!window.confirm("Сбросить аватар? Текущий аватар будет удален.")) {
      return;
    }
    resetting.value = true;
    try {
      const { error } = await PersonalApi.removeMyAvatar();
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

  // Native event handlers — call sites bind через template @drop / @dragover.

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

  // Clipboard paste: глобальный listener на document. Срабатывает на любом
  // Ctrl+V когда страница в фокусе. Если в clipboard есть image — загружаем.
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
    // Event handlers (для template @-bindings)
    onDragEnter,
    onDragOver,
    onDragLeave,
    onDrop,
  };
}
