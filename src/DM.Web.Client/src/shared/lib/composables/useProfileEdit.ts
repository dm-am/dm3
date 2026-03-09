import { ref, computed, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import { PersonalApi, type UpdateProfilePayload } from "@/shared/api";

/**
 * Composable for managing profile edit mode with centralized pending changes.
 * Provides edit toggle, field tracking, and save/cancel functionality.
 */
export function useProfileEdit(targetUsername: Ref<string>) {
  const { user: currentUser } = storeToRefs(useUserStore());

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
    const { [field]: _, ...rest } = pendingChanges.value;
    pendingChanges.value = rest as UpdateProfilePayload;
  }

  async function saveChanges(): Promise<boolean> {
    if (!hasChanges.value) return true;

    isSaving.value = true;
    saveError.value = null;

    const { error } = await PersonalApi.updateMyProfile(pendingChanges.value);

    isSaving.value = false;

    if (error) {
      saveError.value = "Не удалось сохранить изменения";
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
