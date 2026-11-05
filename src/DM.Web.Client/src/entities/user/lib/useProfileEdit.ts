import { ref, computed, getCurrentScope, onScopeDispose, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { unwrapResource, uploadApi } from "@/shared/api";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import type { User, Username } from "@/shared/api/models/community";
import { personalApi, userApi, type UpdateProfilePayload } from "../api";
import type { UploadedAvatar } from "../model/useAvatarUpload";
import { useCommunityStore } from "../model/communityStore";
import { refusalHere } from "@/shared/lib/errors";
import { isSameUsername } from "./roles";
import { fetchUser } from "./session";

/**
 * The owner's edit mode of a profile. Every change, the avatar too, waits in
 * `pendingChanges` and reaches the server on "Сохранить" alone; "Отмена"
 * drops them all.
 */
export function useProfileEdit(targetUsername: Ref<string>) {
  const { user: currentUser } = storeToRefs(useAuthStore());

  const isEditMode = ref(false);
  const pendingChanges = ref<UpdateProfilePayload>({});
  const isSaving = ref(false);
  const saveError = ref<string | null>(null);
  const avatar = usePendingAvatar(pendingChanges);

  const canEdit = computed(() =>
    isSameUsername(currentUser.value?.username, targetUsername.value),
  );

  const hasChanges = computed(
    () => Object.keys(pendingChanges.value).length > 0,
  );

  function cancelEdit() {
    avatar.dropPendingUpload();
    pendingChanges.value = {};
    saveError.value = null;
    isEditMode.value = false;
  }

  function toggleEditMode() {
    if (isEditMode.value) cancelEdit();
    else isEditMode.value = true;
  }

  function setField<K extends keyof UpdateProfilePayload>(
    field: K,
    value: UpdateProfilePayload[K],
  ) {
    pendingChanges.value = { ...pendingChanges.value, [field]: value };
  }

  async function saveChanges(): Promise<boolean> {
    if (!hasChanges.value) return true;

    isSaving.value = true;
    saveError.value = null;
    const avatarChanged = pendingChanges.value.avatarUploadId !== undefined;

    const sent = await sendChanges(pendingChanges);
    if (!sent.stored) {
      // Beside the form, so the reader keeps the fields in view.
      saveError.value = sent.refusal;
      isSaving.value = false;
      return false;
    }

    await showSaved(targetUsername.value, avatarChanged);

    // After the read, so the picked picture holds until the stored one replaces it.
    pendingChanges.value = {};
    avatar.forgetPreview();
    isSaving.value = false;
    isEditMode.value = false;
    return true;
  }

  // A page left in the middle of an edit links nothing it uploaded.
  if (getCurrentScope()) onScopeDispose(avatar.dropPendingUpload);

  return {
    isEditMode,
    canEdit,
    hasChanges,
    isSaving,
    saveError,
    avatarPreview: avatar.avatarPreview,
    toggleEditMode,
    setField,
    setAvatar: avatar.setAvatar,
    removeAvatar: avatar.removeAvatar,
    saveChanges,
    cancelEdit,
    pendingChanges,
  };
}

/** The picture of the edit: an upload the save links, or a reset, and the picture drawn meanwhile. */
function usePendingAvatar(pendingChanges: Ref<UpdateProfilePayload>) {
  /** The picked picture, drawn in place of the saved one until the edit ends. */
  const avatarPreview = ref<string | null>(null);

  function forgetPreview() {
    if (avatarPreview.value) URL.revokeObjectURL(avatarPreview.value);
    avatarPreview.value = null;
  }

  // An upload no save will link: deleted now, its file goes with the sweeper's grace period.
  function dropPendingUpload() {
    const uploadId = pendingChanges.value.avatarUploadId;
    if (uploadId) void uploadApi.deleteUpload(uploadId, OWNS_REFUSAL);
    forgetPreview();
  }

  /** A picture that went up: the save links it, and it shows at once. */
  function setAvatar(uploaded: UploadedAvatar) {
    dropPendingUpload();
    avatarPreview.value = URL.createObjectURL(uploaded.file);
    pendingChanges.value = {
      ...pendingChanges.value,
      avatarUploadId: uploaded.uploadId,
    };
  }

  /**
   * No picture: the save resets the stored one. With none stored, only the
   * pending upload goes, and nothing is left to save of the avatar.
   */
  function removeAvatar(saved: boolean) {
    dropPendingUpload();
    const { avatarUploadId: _dropped, ...rest } = pendingChanges.value;
    pendingChanges.value = saved ? { ...rest, avatarUploadId: null } : rest;
  }

  return {
    avatarPreview,
    forgetPreview,
    dropPendingUpload,
    setAvatar,
    removeAvatar,
  };
}

type Sent = { stored: true } | { stored: false; refusal: string | null };

/** The fields and an upload go by the PATCH; a reset has an endpoint of its own. */
async function sendChanges(pending: Ref<UpdateProfilePayload>): Promise<Sent> {
  const { avatarUploadId, ...fields } = pending.value;
  const resetsAvatar = avatarUploadId === null;
  const patch = resetsAvatar ? fields : pending.value;

  if (Object.keys(patch).length > 0) {
    const { error } = await personalApi.updateMyProfile(patch);
    if (error) {
      return {
        stored: false,
        refusal: refusalHere(error, "Не удалось сохранить профиль"),
      };
    }
  }
  if (resetsAvatar) {
    const { error } = await personalApi.removeMyAvatar();
    if (error) {
      // The fields are stored: the reset alone is left to send again.
      pending.value = { avatarUploadId: null };
      return {
        stored: false,
        refusal: refusalHere(error, "Не удалось сбросить аватар"),
      };
    }
  }
  return { stored: true };
}

/** The page draws the public profile: read again, it shows what was stored, as others see it. */
async function showSaved(username: string, avatarChanged: boolean) {
  const community = useCommunityStore();
  // The name of the address, which the server finds in any case.
  const { data } = await userApi.getUserProfile(username as Username);
  const saved = unwrapResource<User>(data);
  // The reader may have gone to another profile meanwhile.
  if (saved && community.selectedUser?.id === saved.id) {
    community.selectedUser = saved;
  }
  // The picture in the header is the session's. A refresh that throws on a
  // full browser store leaves the header behind, not the save undone.
  if (avatarChanged) await fetchUser().catch(() => undefined);
}
