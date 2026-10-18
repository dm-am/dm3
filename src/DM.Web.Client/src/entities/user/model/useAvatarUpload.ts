import { computed, onMounted, onUnmounted, ref, type Ref } from "vue";
import type { AxiosProgressEvent } from "axios";
import { uploadApi } from "@/shared/api";
import { personalApi } from "../api";
import { fetchUser } from "../lib/session";
import { useCommunityStore } from "./communityStore";
import { compressImage } from "@/shared/lib/utils/imageCompression";
import type { User } from "@/shared/api/models/community/users";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { refusalHere } from "@/shared/lib/errors";

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
 * - After a successful upload/reset — the new picture is written onto the
 *   profile the page draws (community store) and the session user is
 *   refreshed with fetchUser(), plus a SignalR `UserAvatarChanged` broadcast
 *   to open tabs.
 * - Nothing here raises a toast. A change that happened is visible in the
 *   picture itself, and an upload that did not happen goes into `refusal`,
 *   which the caller prints under the picture. A reset that did not happen is
 *   answered to the window that asked before it. See the note on `refusal`
 *   before putting a toast back.
 *
 * @param user — a reactive ref to the User (.id is needed for targetId and to
 *   determine "is there anything to reset").
 */
export function useAvatarUpload(user: Ref<User | null | undefined>) {
  // The profile page draws `selectedUser`, not the session user: the session
  // refresh below reaches the header and nothing else. See
  // applySelectedUserPicture.
  const community = useCommunityStore();

  const uploading = ref(false);
  const resetting = ref(false);
  const progress = ref(0);
  const isDragover = ref(false);

  /**
   * The one sentence the screen says about an upload that did not happen,
   * for the caller to print under the picture. Null while nothing has failed,
   * and again from the moment of the next attempt.
   *
   * A line under the picture and not a toast, on purpose. The press was made
   * on the avatar, by a click, a drop or a paste, and the reader is looking at
   * the avatar for the outcome; a toast answers in the corner of the screen
   * instead, stays until dismissed by hand (an error toast has no timer), and
   * so costs a click. The line stands where the picture is whatever the
   * pointer does after the press, and leaves with the next attempt or with the
   * edit mode - the way a form keeps its refusal line until the next submit.
   */
  const refusal = ref<string | null>(null);

  const hasAvatar = computed(() => {
    const p = user.value?.picture;
    return !!(p?.originalUrl || p?.mediumUrl || p?.smallUrl);
  });

  const busy = computed(() => uploading.value || resetting.value);

  /**
   * Writes a refused request into `refusal`, in the server's own words when
   * it has any (describeFailure), the refusals the interceptor speaks for
   * included: the plate it held for them is taken down, and the line under
   * the picture is the one carrier. Blank for an expired session and a dead
   * connection, which stay with the plate.
   */
  function refuse(error: GeneralError | BadRequestError, fallback: string) {
    refusal.value = refusalHere(error, fallback);
  }

  async function uploadFile(rawFile: File) {
    if (busy.value) return;
    if (!user.value) return;

    // A new attempt takes the previous answer down first: the line is about
    // this try, and a stale one under a running progress bar reads as the
    // verdict on it.
    refusal.value = null;

    if (!ALLOWED_MIME.has(rawFile.type)) {
      // Decided here and not left to the server: a drop and a paste never pass
      // through the accept filter of the picker, and the answer is known
      // before a byte goes up.
      refusal.value = "Для аватара допустимы только JPEG, PNG или WebP";
      return;
    }

    // Read before the first await: the reader may open another profile while
    // the file is still going up, and the answer belongs to this one.
    const userId = user.value.id;

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
          targetId: userId,
          onProgress,
        });
      if (uploadError) {
        refuse(uploadError, "Не удалось загрузить аватар");
        return;
      }

      const { data: profile, error: profileError } =
        await personalApi.updateMyProfile({
          // `data` is nullable in `ApiResult` for the failure branch alone, and
          // the check above has already left it: a successful upload is a 201
          // carrying the record it created (UploadController.DirectUpload
          // answers CreatedAtRoute with it), so past that check there is always
          // an id. A branch for "succeeded with no body" would be a sentence no
          // reader can reach.
          avatarUploadId: uploadData!.id,
        });
      if (profileError) {
        // Not "Не удалось обновить профиль": the reader was changing a picture,
        // not editing a profile, and the word that comes back has to be the one
        // they pressed. Distinct from the failed upload above on purpose —
        // there the file never arrived, here it arrived and the profile refused
        // to take it, and the reader may retry the second one as is.
        refuse(profileError, "Не удалось сохранить аватар");
        return;
      }

      await fetchUser();

      // The PATCH answers with the profile re-read after the write, so the new
      // picture is already here. The big picture on the page and the
      // "Сбросить" button beside it read the community store, which is filled
      // once per route change — without this line both waited for a reload,
      // and asking the server for the profile again would only fetch what this
      // reply already carried.
      if (profile?.picture) {
        community.applySelectedUserPicture(userId, profile.picture);
      }

      // Deliberately no "Аватар обновлен": the picture is replaced in front of
      // the reader, which says it better than a message laid over it. Restore
      // the toast only if the avatar stops changing on the spot.
    } catch {
      // Live, and worth naming, because the API client is not what throws here:
      // it answers every failure with `{ data, error }` and `compressImage`
      // returns the original file on every path that goes wrong. What can throw
      // is `fetchUser()` — it hands the viewer to the auth store, which writes
      // it to localStorage, and setItem raises a DOMException when the storage
      // of the origin is full or blocked for this reader. Then the picture on
      // the page is never applied, so the screen matches the sentence: nothing
      // changed where the reader is looking.
      refusal.value = "Не удалось загрузить аватар";
    } finally {
      uploading.value = false;
      progress.value = 0;
    }
  }

  /**
   * Removes the avatar. Asking first is the job of the caller — confirmation
   * is a UI concern and this composable owns no template.
   *
   * The refusal is answered to the caller and not written into `refusal`: the
   * window that asks first stays open over a refused reset and prints it in
   * itself, so a line under the picture would say the same thing a second
   * time. `options` are that window's flags, which keep the interceptor quiet
   * for the same reason. A throw is the caller's too, and the window counts it
   * as a failure like any other.
   */
  async function resetAvatar(
    options?: Parameters<typeof personalApi.removeMyAvatar>[0],
  ): Promise<{ error: GeneralError | null }> {
    if (!hasAvatar.value || busy.value) return { error: null };
    // The line of a refused upload is about the last try, and this is the next.
    refusal.value = null;
    // hasAvatar is true, so there is a user; read the id before the await for
    // the same reason the upload does.
    const userId = user.value?.id;
    resetting.value = true;
    try {
      const { error } = await personalApi.removeMyAvatar(options);
      if (error) return { error };
      // Can throw: see the catch of uploadFile. Then the silhouette never
      // replaces the picture, and the window that asked says the reset failed.
      await fetchUser();

      // A reset answers with no body and needs none: an empty picture is the
      // whole outcome, and it is what takes the big picture and the "Сбросить"
      // button off the page — both read the profile in the community store.
      // Re-reading that profile would buy the same empty picture for a
      // request.
      if (userId) community.applySelectedUserPicture(userId, {});

      // Deliberately no "Аватар сброшен": the picture gives way to the default
      // silhouette and the "Сбросить" button disappears, both in front of the
      // reader. Restore the toast only if that stops being visible.
      return { error: null };
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
    refusal,
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
