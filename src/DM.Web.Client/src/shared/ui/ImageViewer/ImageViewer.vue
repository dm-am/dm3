<script setup lang="ts">
// Any click closes it: there is nothing in the viewer to press but the way out.
import { computed, ref } from "vue";
import { useDialogShell } from "@/shared/lib/composables/useDialogShell";
import {
  closeImageViewer,
  viewedImage,
} from "@/shared/lib/composables/useImageViewer";

const frame = ref<HTMLElement | null>(null);
const cross = ref<HTMLButtonElement | null>(null);

const { handleKeydown } = useDialogShell({
  show: computed(() => viewedImage.value !== null),
  container: frame,
  initialFocus: () => cross.value,
  onDismiss: closeImageViewer,
});
</script>

<template>
  <Teleport to="body">
    <Transition name="image-viewer">
      <!-- The text it stands in describes the picture, so the copy says nothing of its own. -->
      <div
        v-if="viewedImage"
        ref="frame"
        class="image-viewer"
        role="dialog"
        aria-modal="true"
        aria-label="Картинка целиком"
        tabindex="-1"
        @click="closeImageViewer"
        @keydown="handleKeydown"
      >
        <img
          class="image-viewer-picture"
          :src="viewedImage.src"
          alt=""
          referrerpolicy="no-referrer"
        />
        <button
          ref="cross"
          type="button"
          class="image-viewer-close"
          aria-label="Закрыть"
        ></button>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/DialogSurface" as *

// Above the windows: a picture can be opened from one. margin: auto starts a long picture at the top.
.image-viewer
  position: fixed
  inset: 0
  z-index: $z-popover
  display: flex
  box-sizing: border-box
  padding: $big
  overflow: auto
  overscroll-behavior: contain
  background-color: $overlay-bg
  cursor: zoom-out

.image-viewer-picture
  display: block
  flex: none
  max-width: 100%
  height: auto
  margin: auto

// On the page's fill, so the cross reads over a picture as well as over the dimming.
.image-viewer-close
  +dialog-close

  &
    position: fixed
    top: $medium
    right: $medium
    background-color: $bg-page

// vue-final-modal's own fade.
.image-viewer-enter-active,
.image-viewer-leave-active
  transition: opacity $transition-slow

  @media (prefers-reduced-motion: reduce)
    transition: none

.image-viewer-enter-from,
.image-viewer-leave-to
  opacity: 0
</style>
