import { ref, computed, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/stores";
import { useCommunityStore, type UpdateUserPayload } from "@/stores/community";
import type { UserLogin } from "@/api/models/community";

/**
 * Composable for managing profile edit mode with centralized pending changes.
 * Provides edit toggle, field tracking, and save/cancel functionality.
 */
export function useProfileEdit(targetLogin: Ref<string>) {
  const { user: currentUser } = storeToRefs(useUserStore());
  const communityStore = useCommunityStore();

  const isEditMode = ref(false);
  const pendingChanges = ref<UpdateUserPayload>({});
  const isSaving = ref(false);
  const saveError = ref<string | null>(null);

  const canEdit = computed(
    () => currentUser.value?.login === targetLogin.value,
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

  function setField<K extends keyof UpdateUserPayload>(
    field: K,
    value: UpdateUserPayload[K],
  ) {
    pendingChanges.value = { ...pendingChanges.value, [field]: value };
  }

  function clearField<K extends keyof UpdateUserPayload>(field: K) {
    const { [field]: _, ...rest } = pendingChanges.value;
    pendingChanges.value = rest as UpdateUserPayload;
  }

  async function saveChanges(): Promise<boolean> {
    if (!hasChanges.value) return true;

    isSaving.value = true;
    saveError.value = null;

    const { error } = await communityStore.updateUser(
      targetLogin.value as UserLogin,
      pendingChanges.value,
    );

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

  function getFieldValue<K extends keyof UpdateUserPayload>(
    field: K,
  ): UpdateUserPayload[K] | undefined {
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
