<script setup lang="ts">
import { VueFinalModal } from "vue-final-modal";
</script>

<template>
  <vue-final-modal
    content-class="lightbox"
    overlay-transition="vfm-fade"
    content-transition="vfm-fade"
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

.lightbox
  position: relative
  padding: $medium
  width: $grid-step * 100
  border-radius: $border-radius
  background-color: $bg-page

  & h2
    margin-top: 0

.lightbox-close
  position: absolute
  top: 0
  right: 0
  margin: $small

  font-size: $grid-step * 4
  cursor: pointer
  color: $text

  &:hover
    color: $link

h2
  margin-top: 0
</style>
