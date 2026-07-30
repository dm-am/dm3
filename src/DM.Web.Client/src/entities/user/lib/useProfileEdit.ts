import { ref, computed, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { personalApi, type UpdateProfilePayload } from "../api";
import { describeFailure } from "@/shared/lib/errors";

/**
 * Composable for managing profile edit mode with centralized pending changes.
 * Provides edit toggle, field tracking, and save/cancel functionality.
 */
export function useProfileEdit(targetUsername: Ref<string>) {
  const { user: currentUser } = storeToRefs(useAuthStore());

  const isEditMode = ref(false);
  const pendingChanges = ref<UpdateProfilePayload>({});
  const isSaving = ref(false);
  const saveError = ref<string | null>(null);

  const canEdit = computed(
    () => currentUser.value?.username === targetUsername.value,
  );

  const hasChanges = computed(
    () => Object.keys(pendingChanges.value).length > 0,
  );

  function toggleEditMode() {
    if (isEditMode.value) {
      pendingChanges.value = {};
      saveError.value = null;
    }
    isEditMode.value = !isEditMode.value;
  }

  function setField<K extends keyof UpdateProfilePayload>(
    field: K,
    value: UpdateProfilePayload[K],
  ) {
    pendingChanges.value = { ...pendingChanges.value, [field]: value };
  }

  function clearField<K extends keyof UpdateProfilePayload>(field: K) {
    const rest = { ...pendingChanges.value };
    delete rest[field];
    pendingChanges.value = rest;
  }

  async function saveChanges(): Promise<boolean> {
    if (!hasChanges.value) return true;

    isSaving.value = true;
    saveError.value = null;

    const { error } = await personalApi.updateMyProfile(pendingChanges.value);

    isSaving.value = false;

    if (error) {
      // Shown next to the form rather than as a toast, so the reader keeps the
      // fields in view. The server's own sentence wins: it names the field and
      // the rule, where the fallback only says that something went wrong.
      saveError.value = describeFailure(
        error,
        "Не удалось сохранить изменения",
      );
      return false;
    }

    pendingChanges.value = {};
    isEditMode.value = false;
    return true;
  }

  function cancelEdit() {
    pendingChanges.value = {};
    saveError.value = null;
    isEditMode.value = false;
  }

  function getFieldValue<K extends keyof UpdateProfilePayload>(
    field: K,
  ): UpdateProfilePayload[K] | undefined {
    return pendingChanges.value[field];
  }

  return {
    isEditMode,
    canEdit,
    hasChanges,
    isSaving,
    saveError,
    toggleEditMode,
    setField,
    clearField,
    saveChanges,
    cancelEdit,
    getFieldValue,
    pendingChanges,
  };
}
