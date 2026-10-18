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
 *
 * Two roots: the shade over the picture, and under the picture the line that
 * says why a change did not happen. See the note at that line.
 */
defineProps<{
  username: string;
}>();

// Take the user from the community store: `selectedUser` is the object the
// profile page draws. It is not the session user — the same person, a separate
// object, filled once per route change — so an upload or a reset has to be
// written back into this store, which is what useAvatarUpload does.
const communityStore = useCommunityStore();
const selectedUser = computed(() => communityStore.selectedUser);

const avatar = useAvatarUpload(selectedUser);

const stateLabel = computed(() => {
  if (avatar.uploading.value) return `${avatar.progress.value}%`;
  if (avatar.resetting.value) return "Сброс...";
  if (avatar.isDragover.value) return "Отпустите для загрузки";
  return "Загрузить (перетащите или вставьте)";
});

const handleUploaded = async (formData: FormData) => {
  const file = formData.get("file") as File | null;
  if (file) await avatar.uploadFile(file);
};

// --- Reset avatar (ConfirmDialog-gated) ---
const confirmingReset = ref(false);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
const confirmReset = () =>
  avatar.resetAvatar({ ownsRefusal: true, ownsRateLimit: true });
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
      title="Сбросить аватар?"
      message="Текущий аватар будет удален"
      confirm-label="Сбросить"
      failure="Не удалось сбросить аватар"
      :action="confirmReset"
    />
  </div>

  <!-- The refusal of an upload, under the picture and outside the shade
       above, in the words and colour of the form refusal line (p.form-error
       elsewhere). A refused reset is not here: the window that asked before
       it stays open and says it. Not inside the shade: that one is drawn on
       hover alone, so a line in it is gone on a touch screen and after a
       paste made with the pointer elsewhere on the page - and the red does
       not read on the dark shade in the light theme (1.2:1 over a light
       photo). Not a toast either: it would
       answer in the corner for a press made on the avatar, and stay until
       dismissed by hand. A second root in the flow of .avatar-wrapper stands
       under the picture whatever the pointer does, and goes down with the
       next attempt. The drag handlers are bound here too: the line is part of
       the drop target, and a file dropped on it with no handler is opened by
       the browser in place of the page. -->
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
@use "@/assets/styles/Inputs" as *

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
  // Square: the shade lies over the picture, which is square (a picture is
  // content, and a rounded shade left its corners standing out of it).
  cursor: pointer

.upload-label
  color: $shade-text
  font-weight: bold
  text-transform: uppercase
  pointer-events: none
  text-align: center
  padding: 0 $small
  font-size: $secondary-font-size

// A link-button of the site on the dark shade: the ink of the shade at rest,
// and under the pointer the hover blue drawn for dark surfaces (the tooltip's),
// with the underline. The padding is its pointer target over the picture.
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
  height: 4px
  background-color: $accent-green
  transition: width 0.2s ease

// In the flow of .avatar-wrapper, which is ProfilePage's and is also the
// shade's containing block: once this line is in it the wrapper grows, the
// shade grows with it, and on hover the words would sit under the dark
// overlay. Positioned, the line paints above the shade (a positioned sibling
// later in the tree); on the page colour, which is what the identity block
// sits on, it covers the shade under it; padding rather than a top margin
// keeps that cover joined to the picture's bottom edge, or the shade shows
// through the gap. The clean shape is a positioned box around the picture
// alone, and that box is ProfilePage's to draw.
.avatar-refusal
  position: relative
  margin: 0
  padding-top: $small
  background-color: $bg-page
  color: $accent-red
  font-size: $secondary-font-size
</style>
