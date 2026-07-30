<script setup lang="ts">
import { computed, ref } from "vue";
import {
  useCommunityStore,
  useAvatarUpload,
  AVATAR_ACCEPT,
} from "@/entities/user";
import { UploadInput } from "@/features/upload";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";

/**
 * Overlay for editing the avatar on the profile page.
 * Shown only when canEdit && isEditMode (see ProfilePage.vue).
 * The single UX entry point for avatar editing in the app.
 *
 * Uses the `useAvatarUpload` composable — the SSOT for upload/reset logic,
 * drag-drop, paste, compression, progress.
 */
defineProps<{
  username: string;
}>();

// Take the user from the community store (it holds the profile's selectedUser). If
// the current user opened their own profile, it is the same object as in userStore.
const communityStore = useCommunityStore();
const selectedUser = computed(() => communityStore.selectedUser);

const avatar = useAvatarUpload(selectedUser);

const stateLabel = computed(() => {
  if (avatar.uploading.value) return `${avatar.progress.value}%`;
  if (avatar.resetting.value) return "Сброс...";
  if (avatar.isDragover.value) return "Отпустите для загрузки";
  return "Загрузить — перетащите или вставьте";
});

const handleUploaded = async (formData: FormData) => {
  const file = formData.get("file") as File | null;
  if (file) await avatar.uploadFile(file);
};

// --- Reset avatar (ConfirmDialog-gated) ---
const confirmingReset = ref(false);

const confirmReset = async () => {
  await avatar.resetAvatar();
  confirmingReset.value = false;
};
</script>

<template>
  <div
    class="profile-picture-upload"
    :class="{
      'is-active': avatar.uploading.value || avatar.resetting.value,
      'is-dragover': avatar.isDragover.value,
    }"
    @dragenter="avatar.onDragEnter"
    @dragover="avatar.onDragOver"
    @dragleave="avatar.onDragLeave"
    @drop="avatar.onDrop"
  >
    <div class="upload-overlay">
      <span class="upload-label">{{ stateLabel }}</span>
      <UploadInput
        v-if="!avatar.uploading.value && !avatar.resetting.value"
        :accept="AVATAR_ACCEPT"
        @uploading="handleUploaded"
      />
      <button
        v-if="avatar.hasAvatar.value && !avatar.uploading.value"
        type="button"
        class="reset-btn"
        :disabled="avatar.resetting.value"
        @click.stop="confirmingReset = true"
      >
        Сбросить
      </button>
    </div>
    <div
      v-if="avatar.uploading.value"
      class="upload-progress"
      :style="{ width: `${avatar.progress.value}%` }"
      aria-hidden="true"
    />

    <ConfirmDialog
      v-model:show="confirmingReset"
      title="Сброс аватара"
      message="Сбросить аватар? Текущий аватар будет удален."
      confirm-label="Сбросить"
      danger
      :loading="avatar.resetting.value"
      @confirm="confirmReset"
    />
  </div>
</template>

<style scoped lang="sass">
.profile-picture-upload
  position: absolute
  top: 0
  left: 0
  right: 0
  bottom: 0
  opacity: 0
  transition: opacity 0.2s ease

  &:hover,
  &.is-active,
  &.is-dragover
    opacity: 1

  &.is-dragover .upload-overlay
    outline: 2px dashed $accent-green
    outline-offset: -4px

.upload-overlay
  position: absolute
  top: 0
  left: 0
  right: 0
  bottom: 0
  background-color: $shade-bg
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  gap: $small
  border-radius: $border-radius
  cursor: pointer

.upload-label
  color: $shade-text
  font-weight: bold
  text-transform: uppercase
  pointer-events: none
  text-align: center
  padding: 0 $small
  font-size: $secondary-font-size

.reset-btn
  background: transparent
  border: none
  color: $shade-text
  font-size: $secondary-font-size
  text-decoration: underline
  cursor: pointer
  padding: $tiny $small
  pointer-events: auto

  &:hover:not(:disabled)
    color: $accent-red

  &:disabled
    cursor: not-allowed
    opacity: 0.5

.upload-progress
  position: absolute
  left: 0
  bottom: 0
  height: 4px
  background-color: $accent-green
  transition: width 0.2s ease
</style>
