<script setup lang="ts">
/**
 * SecondaryText — a grey line of the second rank: an empty state
 * ("Комментариев пока нет"), a caption, a meta line. Not a refusal: a line
 * saying the reader may not write here, or must sign in first, is a plaque
 * (RefusalNote), so the two never read alike.
 */
</script>

<template>
  <div class="secondary-text"><slot /></div>
</template>

<style scoped lang="sass">
.secondary-text
  font-size: $secondary-font-size
  font-weight: normal
  color: $text-muted
</style>
