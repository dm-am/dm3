<script setup lang="ts">
/**
 * ImageViewer — a picture of a text, whole, over the dimmed page.
 *
 * A picture in a post is drawn no taller than the window allows (the image
 * rules of _BbcodeContent.sass), and a map or a character sheet taller than
 * that could not be read at all: it was squeezed into the post and there was
 * no way to see it larger. A click on it opens it here at its own size, or at
 * the width of the window if it is wider; a long one scrolls. The owner's
 * word, 27 September 2026.
 *
 * Any click closes it, on the picture as well as beside it: there is nothing
 * in the viewer to press but the way out. So do the cross and Escape. The
 * caret starts on the cross and cannot leave the viewer while it is open, and
 * the page under it stands still (useDialogShell), as under every window of
 * the site; the end of a session closes it with the windows.
 *
 * Drawn once by the app. What it shows comes from useImageViewer, which the
 * text fills in on a click (bbcodeInteractive.ts).
 */
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
  holdPage: true,
});
</script>

<template>
  <Teleport to="body">
    <Transition name="image-viewer">
      <!-- A picture of the text, repeated whole: the text it stands in is
           what describes it, so the copy says nothing of its own. -->
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
        <!-- Empty on purpose: the two bars of the cross are drawn by the
             stylesheet, as on every window of the site. -->
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

// The whole screen over the dimming the site's windows stand on, and the one
// thing that scrolls. Above the windows, since a picture can be opened from
// one (the examples of the markup help).
//
// `margin: auto` on the picture centres one that fits and starts one that does
// not at the top, where centring would cut a long picture at both ends.
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

// The cross of a window, in the corner of the screen, on the fill of the page
// so it reads over a picture as well as over the dimming.
.image-viewer-close
  +dialog-close

  &
    position: fixed
    top: $medium
    right: $medium
    background-color: $bg-page

// The fade of the site's windows (vue-final-modal's own, 0.3s).
.image-viewer-enter-active,
.image-viewer-leave-active
  transition: opacity 0.3s ease

  @media (prefers-reduced-motion: reduce)
    transition: none

.image-viewer-enter-from,
.image-viewer-leave-to
  opacity: 0
</style>
