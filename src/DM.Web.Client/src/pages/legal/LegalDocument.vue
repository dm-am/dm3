<script setup lang="ts">
/** The layout and the revision line both legal documents share. */

defineProps<{
  title: string;
}>();

// Both documents are revised together, so one date stands for both.
const REVISION_DATE = "31 марта 2026 года";
</script>

<template>
  <div class="legal-document">
    <page-title v-once>{{ title }}</page-title>

    <div class="legal-content" v-once>
      <p class="revision-date">Редакция от {{ REVISION_DATE }}</p>

      <slot />
    </div>
  </div>
</template>

<style scoped lang="sass">
// Section headings read better in the body color than in the accent of the h1.
.legal-content
  color: $text
  line-height: 1.6

  :deep(section)
    margin-bottom: $big

  :deep(h2)
    margin: $medium 0 $small
    font-weight: bold
    color: $text

  :deep(h3)
    margin: $small 0 $tiny
    font-weight: bold
    color: $text

  :deep(p)
    margin: 0 0 $small

  :deep(ul)
    margin: $tiny 0 $small
    padding-left: $big

    li
      margin: $tiny 0

  // A <dl> of terms drawn exactly like the bulleted list above: "Термин — определение" per item.
  :deep(dl.term-list)
    margin: $tiny 0 $small
    padding-left: $big

    .term-item
      display: list-item
      list-style: disc
      margin: $tiny 0

    dt,
    dd
      display: inline
      margin: 0
      font-weight: inherit

  // A bold link of the site: 700, the one bold of its face.
  :deep(a)
    font-weight: 700

.revision-date
  color: $text-muted
  font-style: italic
  margin-bottom: $medium
</style>
