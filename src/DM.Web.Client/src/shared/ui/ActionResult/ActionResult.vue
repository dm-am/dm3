<script setup lang="ts">
/**
 * ActionResult — the line a place on the page says about the last press made
 * there: a refusal, in red, under the button, the row or the form it is about.
 *
 * It replaces the plate in the corner for every press that has a place on the
 * screen (the owner's rule of 26 September 2026; UI_STANDARDS, "Всплывающие
 * сообщения"). The look is the refusal line the site already had under its
 * forms (CharacterForm, FundraisingPage): red, the secondary size, no box, no
 * mark, starting at the left edge of the place. One step down from what it answers,
 * unless the place spaces its rows itself (a flex column with a gap), which
 * then sets --action-result-margin to nothing. A custom property rather than
 * a margin of the caller's: two rules of one weight on one element would be
 * decided by the order the stylesheets happen to load in.
 *
 * The words come from a place's useActionResult, or, for a place that keeps
 * its own refusal (a line it had before, a refusal per row), as `text`.
 * `tag` is for where a paragraph cannot stand: an item of a list, a span
 * inside a line of words.
 */
import { computed } from "vue";
import type { ActionResult } from "@/shared/lib/composables/useActionResult";

const props = withDefaults(
  defineProps<{
    /** The place's result, from useActionResult. */
    result?: ActionResult;
    /** The line itself, for a place that keeps its own refusal. */
    text?: string | null;
    /** The element the line is: a paragraph, an item of a list, a span. */
    tag?: "p" | "li" | "span" | "div";
  }>(),
  { result: undefined, text: null, tag: "p" },
);

const line = computed(() => props.result?.refusal.value || props.text || "");
</script>

<template>
  <component :is="tag" v-if="line" class="action-result" role="alert">
    {{ line }}
  </component>
</template>

<style scoped lang="sass">
// The refusal line of a form of the site (CharacterForm).
.action-result
  margin: var(--action-result-margin, #{$small} 0 0)
  color: $accent-red
  font-size: $secondary-font-size
</style>
