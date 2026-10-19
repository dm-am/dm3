<script setup lang="ts">
// An empty setup still names the component, and tests stub it by that name.
</script>

<template>
  <div class="button-row">
    <slot />
  </div>
</template>

<style scoped lang="sass">
// No column, no "…", no menu: a button with no room goes down a line, as text does.
.button-row
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  min-width: 0

  // No item is wider than the row: a caption wider than it breaks between words.
  > :deep(*)
    flex-shrink: 0
    max-width: 100%

  :deep(button)
    max-width: 100%
    white-space: normal
    text-align: center
</style>
