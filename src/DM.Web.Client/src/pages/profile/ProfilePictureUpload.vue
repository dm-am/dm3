<script setup lang="ts">
import { computed } from "vue";
import {
  useAvatarUpload,
  AVATAR_ACCEPT,
  type UploadedAvatar,
} from "@/entities/user";
import { UploadInput } from "@/features/upload";

/**
 * The overlay that edits the avatar, in the owner's edit mode, on useAvatarUpload.
 * A picture sent up and a reset are changes of the edit: the page holds them
 * until "Сохранить". Two roots: the shade over the picture, and the refusal
 * line under it.
 */
const props = defineProps<{
  /** The owner, whose upload the server files under him. */
  userId: string;
  /** The picture drawn, the pending change included: whether there is one to reset. */
  hasAvatar: boolean;
}>();

const emit = defineEmits<{
  uploaded: [avatar: UploadedAvatar];
  removed: [];
}>();

const avatar = useAvatarUpload(
  () => props.userId,
  (uploaded) => emit("uploaded", uploaded),
);

const stateLabel = computed(() => {
  if (avatar.uploading.value) return `${avatar.progress.value}%`;
  if (avatar.isDragover.value) return "Отпустите для загрузки";
  return "Загрузить (перетащите или вставьте)";
});

const handleUploaded = async (formData: FormData) => {
  const file = formData.get("file") as File | null;
  if (file) await avatar.uploadFile(file);
};
</script>

<template>
  <div
    class="profile-picture-upload"
    :class="{
      'is-active': avatar.uploading.value,
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
        v-if="!avatar.uploading.value"
        :accept="AVATAR_ACCEPT"
        @uploading="handleUploaded"
      />
      <!-- Asks nothing: the reset waits for "Сохранить", and "Отмена" takes it back. -->
      <button
        v-if="hasAvatar && !avatar.uploading.value"
        type="button"
        class="reset-btn"
        @click.stop="emit('removed')"
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
  </div>

  <!-- A refused upload, under the picture: the shade shows on hover only, and the
       line goes with the next attempt. It is part of the drop target, or a file
       dropped on it opens in place of the page. -->
  <p
    v-if="avatar.refusal.value"
    class="avatar-refusal"
    role="alert"
    @dragenter="avatar.onDragEnter"
    @dragover="avatar.onDragOver"
    @dragleave="avatar.onDragLeave"
    @drop="avatar.onDrop"
  >
    {{ avatar.refusal.value }}
  </p>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

$drop-ring-width: 2px
$progress-height: 4px

.profile-picture-upload
  position: absolute
  inset: 0
  opacity: 0
  transition: opacity $transition-normal

  &:hover,
  &.is-active,
  &.is-dragover
    opacity: 1

  &.is-dragover .upload-overlay
    outline: $drop-ring-width dashed $accent-green
    outline-offset: -$minor

.upload-overlay
  position: absolute
  inset: 0
  background-color: $shade-bg
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  gap: $small
  // Square, as the picture it lies over.
  cursor: pointer

.upload-label
  color: $shade-text
  font-weight: bold
  text-transform: uppercase
  pointer-events: none
  text-align: center
  padding: 0 $small
  font-size: $secondary-font-size

// A link-button on the dark shade: its ink at rest, the tooltip's hover blue under the pointer.
.reset-btn
  pointer-events: auto
  background: none
  border: none
  font: inherit
  cursor: pointer
  +link-tone($shade-text, $tooltip-link-hover)
  &
    padding: $tiny $small
    font-size: $secondary-font-size

.upload-progress
  position: absolute
  left: 0
  bottom: 0
  height: $progress-height
  background-color: $accent-green
  transition: width $transition-normal

// In the flow of .avatar-wrapper, positioned over the shade that grows with it,
// on the page colour, padded rather than margined so the shade does not show through.
.avatar-refusal
  position: relative
  margin: 0
  padding-top: $small
  background-color: $bg-page
  color: $accent-red
  font-size: $secondary-font-size
</style>
