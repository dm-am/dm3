<script setup lang="ts">
/**
 * LegalDocument - shared wrapper for static legal pages (privacy, agreement).
 * Owns the .legal-content layout and the revision/publication date lines so
 * both documents stay visually in sync. The pages themselves are the SSOT
 * for the legal text.
 */

/**
 * Section descriptor used by the legal pages: each page maps its own
 * `sections` array over its `<section>` blocks, so ids and heading titles
 * live in exactly one place.
 */
export interface LegalSection {
  /** Stable kebab-case id used as the `<section id>` anchor. */
  id: string;
  /** Section heading text, e.g. "1. Общие положения". */
  title: string;
}

defineProps<{
  /** Title rendered as the page heading. */
  title: string;
  /** Revision date line shown above the content, e.g. "31 марта 2026 года". */
  revisionDate: string;
  /** Publication date line shown below the content. */
  publicationDate: string;
}>();
</script>

<template>
  <div class="legal-document">
    <page-title v-once>{{ title }}</page-title>

    <div class="legal-content" v-once>
      <p class="revision-date">Редакция от {{ revisionDate }}</p>

      <slot />

      <p class="publication-date">Дата публикации: {{ publicationDate }}</p>
    </div>
  </div>
</template>

<style scoped lang="sass">
// The page title (h1) keeps the accent heading color; only the in-document
// section headings (h2/h3, e.g. "1. Общие положения") use the plain body
// color — they read better in flat black for long-form legal text.
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

  // Glossary definition list ("Термины и определения"). Semantically a
  // <dl> of term/definition pairs, but rendered to look exactly like the
  // bulleted <ul> above: each grouping div is a list-item (disc marker in
  // the same $big padding well), with the term and definition flowing
  // inline as one "Термин — определение" line.
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

  :deep(a)
    color: $link
    font-weight: 600
    &:hover
      color: $link-hover
      text-decoration: underline

.revision-date
  color: $text-muted
  font-style: italic
  margin-bottom: $medium

.publication-date
  color: $text-muted
  font-style: italic
  margin-top: $big
</style>
