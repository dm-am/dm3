<script setup lang="ts">
import { computed } from "vue";
import { VueFinalModal } from "vue-final-modal";

const props = defineProps<{
  narrow?: boolean;
  withForm?: boolean;
  /** Shrink-to-content width (e.g. short confirmation dialogs), capped at
   * 320px, instead of the fixed 580px/380px width tiers. */
  auto?: boolean;
}>();

const emit = defineEmits<{
  (e: "beforeClose"): void;
}>();

const contentClass = computed(() => {
  if (props.auto) return "dialog dialog--auto";
  return props.narrow ? "dialog dialog--narrow" : "dialog";
});
</script>

<template>
  <vue-final-modal
    :content-class="contentClass"
    overlay-class="dialog-overlay"
    overlay-transition="vfm-fade"
    content-transition="vfm-fade"
    :click-to-close="true"
    :esc-to-close="true"
    :lock-scroll="true"
    role="dialog"
    aria-modal="true"
    @before-close="emit('beforeClose')"
  >
    <slot />
  </vue-final-modal>
</template>

<style lang="sass">
@use "@/assets/styles/ZIndex" as *

.vfm
  display: flex
  justify-content: center
  align-items: center
  // Align vfm modals with the self-rolled dialog tier (ConfirmDialog,
  // BBCodeEditor, InputDialog all sit at $z-dialog).
  z-index: $z-dialog

.dialog-overlay
  /* styled via ThemeVariables.css for proper cascade */

.dialog
  position: relative
  padding: $medium
  width: 580px
  // The width tiers are fixed numbers, and seventeen files open a dialog on
  // them — moderation forms among them, which a phone reaches. On a 375px
  // viewport 580 is not a dialog, it is a page running off both edges. The cap
  // leaves the page's own margin on either side and applies to the narrow tier
  // too, which shares this class; the auto tier declares its own below.
  max-width: calc(100vw - #{$medium} * 2)
  background-color: $bg-page
  border-radius: $border-radius

  & h2
    margin-top: 0

.dialog--narrow
  width: 380px

  .form-field-row input,
  .form-field-row textarea,
  .form-field-row select
    width: 100%
    box-sizing: border-box

.dialog--auto
  width: auto
  max-width: 320px
</style>
