<script setup lang="ts">
/**
 * RemoveButton — the flat link-button ("Удалить" / "Отменить") used in
 * roster-style lists across game/blog settings. Extracted so the shared
 * `.remove-btn` styling has a single home. Label comes from the default slot.
 *
 * In the colour of a link and not in red (owner's standard, 27 September
 * 2026): the danger of the action is spelled out by the confirmation it opens,
 * as it is in every window of the site, and a red word in a row was the one
 * coloured action left on a page.
 */
</script>

<template>
  <button type="button" class="remove-btn">
    <slot />
  </button>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.remove-btn
  +inline-link-button
  &
    font-size: $secondary-font-size
</style>
