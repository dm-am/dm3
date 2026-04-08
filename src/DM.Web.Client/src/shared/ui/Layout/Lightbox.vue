<script setup lang="ts">
import { computed } from "vue";
import { VueFinalModal } from "vue-final-modal";

const props = defineProps<{
  narrow?: boolean;
  withForm?: boolean;
}>();

const emit = defineEmits<{
  (e: "beforeClose"): void;
}>();

const contentClass = computed(() =>
  props.narrow ? "lightbox lightbox--narrow" : "lightbox",
);
</script>

<template>
  <vue-final-modal
    :content-class="contentClass"
    overlay-class="lightbox-overlay"
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
@import "src/assets/styles/Themes"

.vfm
  display: flex
  justify-content: center
  align-items: center

.lightbox-overlay
  /* styled via ThemeVariables.css for proper cascade */

.lightbox
  position: relative
  padding: $medium
  width: 580px
  background-color: $bg-page
  border-radius: $border-radius

  & h2
    margin-top: 0

.lightbox--narrow
  width: 380px

  .form-field-row input,
  .form-field-row textarea,
  .form-field-row select
    width: 100%
    box-sizing: border-box
</style>
