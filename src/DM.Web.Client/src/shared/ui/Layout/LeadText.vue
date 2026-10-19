<script setup lang="ts">
// An empty setup still names the component, and tests stub it by that name.
</script>

<template>
  <p class="lead-text"><slot /></p>
</template>

<style scoped lang="sass">
.lead-text
  margin: 0 0 $medium
  // Ordinary body text — same color and size as the page content, not a
  // muted/small caption. Lead-in description above a list/table.
  color: $text
  line-height: 1.6
  // Bold, so the call to action stands out inside the sentence.
  :deep(a)
    font-weight: 700
</style>
