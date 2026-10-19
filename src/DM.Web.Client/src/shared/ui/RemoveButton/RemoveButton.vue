<script setup lang="ts">
/** A link's colour, not red: the confirmation it opens carries the danger. */
</script>

<template>
  <button type="button" class="remove-btn">
    <slot />
  </button>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.remove-btn
  +inline-link-button
  &
    font-size: $secondary-font-size
</style>
