<script setup lang="ts">
/**
 * The board strip of the board and topic pages, the twin of the profile Tabs
 * strip: links separated by " | ", the active board first, its moves animated
 * by FLIP (the strip lives in the persistent ForumPage shell). The active
 * board comes from the route alias, never a stale selectedBoard. The strip
 * wraps at the space after a bar and never scrolls; a board and its bar are
 * one unbreakable item, since Chrome breaks after an inline-block whatever text
 * follows it.
 */
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { storeToRefs } from "pinia";
import { useFlipReorder } from "@/shared/lib/composables/useFlipReorder";

const route = useRoute();
const { boards, boardsError } = storeToRefs(useBoardsStore());

// The route param names the board on every forum level; on the index it is absent.
const activeAlias = computed(() => route.params.alias as string | undefined);

// The active board first; the same key keeps the DOM node, which FLIP measures.
const orderedBoards = computed(() => {
  const list = boards.value ?? [];
  const i = list.findIndex((b) => b.alias === activeAlias.value);
  if (i <= 0) return list;
  return [list[i], ...list.slice(0, i), ...list.slice(i + 1)];
});

const root = ref<HTMLElement | null>(null);

// Focus lands back on the clicked link if the patch moved it.
useFlipReorder({
  root,
  itemSelector: ".board-link",
  focusSelector: ".board-link.active",
});

/** Whether a board has another after it, and so a bar. */
const hasNext = (idx: number) => idx < orderedBoards.value.length - 1;
</script>

<template>
  <nav
    v-if="orderedBoards.length"
    ref="root"
    class="board-navigation"
    aria-label="Разделы форума"
  >
    <!-- One inline run, the separators real text, so a copy reads on one line;
         the space after a bar is the strip's only wrap opportunity. -->
    <template v-for="(board, idx) in orderedBoards" :key="board.id"
      ><span class="board-item"
        ><router-link
          :to="{ name: 'forum', params: { alias: board.alias } }"
          :class="['board-link', { active: activeAlias === board.alias }]"
          :aria-current="activeAlias === board.alias ? 'page' : undefined"
          >{{ board.title }}</router-link
        ><span v-if="hasNext(idx)" class="separator" aria-hidden="true">{{
          " |"
        }}</span></span
      ><span v-if="hasNext(idx)" class="separator" aria-hidden="true">{{
        " "
      }}</span></template
    >
  </nav>
  <!-- Until the boards are read, an empty line of the strip's own height holds
       its place, so the page under it does not jump when the names come. A
       failed read brings no names, so it holds nothing. -->
  <div
    v-else-if="!boards && !boardsError"
    class="board-navigation board-navigation-placeholder"
    aria-hidden="true"
  >
    <span class="board-link">&nbsp;</span>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// The 11 boards must stay on one line at typical widths.
$strip-font-size: 15px

// Inline, not flex: flex blockifies the items, and a copy would read a column.
.board-navigation
  display: block
  // The strip-to-content distance the leaves had before the strip moved into the shell.
  margin-bottom: $medium
  // A sub-pixel shave so the strip holds one line a little longer.
  letter-spacing: -0.01em

.separator
  color: $text-muted
  font-size: $strip-font-size
  // The visible width of the spaces around the bar: two literal spaces collapse
  // and a flex gap breaks the copy, so this is the one knob.
  word-spacing: 2px

.board-item
  white-space: nowrap

.board-link
  display: inline-block
  // Titles never break inside; the strip wraps at the separator spaces.
  white-space: nowrap
  font-size: $strip-font-size
  +link-tone($link)
  // Active: the link colour and the one bold; underlines never mark the active item.
  &.active
    font-weight: 700
  // Keyboard focus is the text link's ring from Reset.sass.
</style>
