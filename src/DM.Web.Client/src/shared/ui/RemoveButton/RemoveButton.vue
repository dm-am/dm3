<script setup lang="ts">
/**
 * RemoveButton — the flat red link-button ("Удалить" / "Отменить") used in
 * roster-style lists across game/blog settings. Extracted so the shared
 * `.remove-btn` styling has a single home. Label comes from the default slot.
 */
</script>

<template>
  <button type="button" class="remove-btn">
    <slot />
  </button>
</template>

<style scoped lang="sass">
.remove-btn
  padding: 0
  border: none
  background: none
  font: inherit
  font-size: $secondary-font-size
  color: $accent-red
  cursor: pointer

  &:hover
    text-decoration: underline
</style>
