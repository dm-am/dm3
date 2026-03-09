<script setup lang="ts">
// Standalone lightbox for pages that should look like modals (email-initiated flows)
// Unlike TheLightbox (vue-final-modal), this is a regular component for router pages
</script>

<template>
  <div class="standalone-lightbox-overlay">
    <div class="lightbox">
      <slot />
    </div>
  </div>
</template>

<style lang="sass">
@import "src/assets/styles/Themes"

.standalone-lightbox-overlay
  min-height: 100vh
  display: flex
  justify-content: center
  align-items: flex-start
  padding: $big
  background: var(--overlay-bg)

// .lightbox styles are inherited from TheLightbox.vue (global)
</style>
