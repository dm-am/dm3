<template>
  <p class="lead-text"><slot /></p>
</template>

<script setup lang="ts"></script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.lead-text
  margin: 0 0 $medium
  // Ordinary body text — same color and size as the page content, not a
  // muted/small caption. Lead-in description above a list/table.
  color: $text
  line-height: 1.6
  // Links read as ordinary links (normal link color + standard hover) but
  // bold, so the call-to-action stands out inside the sentence.
  :deep(a)
    color: $link
    font-weight: 700
    text-decoration: none
    &:hover
      color: $link-hover
      text-decoration: underline
</style>
