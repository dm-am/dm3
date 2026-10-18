<script setup lang="ts">
/**
 * BoardNavigation — board strip shared by the board and topic pages
 * (the /forum index navigates with its boards table instead).
 *
 * Design: a compact left-aligned row separated by " | ", in 15px (kept a
 * notch below the profile Tabs' $font-size so the 11-board strip fits one line)
 * typography. Items behave like the site's regular links: `$link` color,
 * `$link-hover` + underline on hover; the active item keeps the link
 * color and is marked by semibold weight (underline only on hover, never
 * as an active indicator). The same visual contract is shared with the
 * profile Tabs strip (see shared/ui/Tabs/Tabs.vue) — the two must look
 * identical.
 *
 * Active-first behavior (same as Tabs): the current board is always
 * rendered at visual (and DOM) index 0; the rest follow in their declared
 * order. The strip lives in the persistent ForumPage shell — it is NOT
 * remounted between forum pages — so when the active board changes, the
 * shared FLIP pass (useFlipReorder) animates each moved link from its old
 * bounding box to the new one. Only the links are animated: the " | "
 * separators are visually identical glyphs, so they simply re-render at
 * their new slots without drawing attention.
 *
 * The active board is derived from the route alias rather than the
 * store's selectedBoard: the alias always matches the page being viewed,
 * while a stale selectedBoard from a previous visit could highlight the
 * wrong item.
 *
 * Every width wraps the strip at the separators, a phone's too: a row of the
 * page never scrolls sideways (owner, 12 October 2026), so where the column
 * is narrow the strip takes the lines it needs. A board and the bar after it
 * are one unbreakable item, and the line breaks at the space after the bar
 * only: a bar closes a line and never opens one. A no-break space before the
 * bar would not do it, since the link is an inline-block (it has to be, for
 * the FLIP transform), and Chrome breaks after an inline-block whatever text
 * follows it.
 */
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { useBoardsStore } from "@/entities/forum";
import { storeToRefs } from "pinia";
import { useFlipReorder } from "@/shared/lib/composables/useFlipReorder";

const route = useRoute();
const { boards } = storeToRefs(useBoardsStore());

// Board and topic pages both live under /forum/:alias(/:num), so the route
// param identifies the current board on every forum level; on the /forum
// index it is absent and no item renders as active.
const activeAlias = computed(() => route.params.alias as string | undefined);

// Visual / DOM order: active board first, rest preserve their declared
// order. Same `:key="board.id"` for the same item across renders means Vue
// reuses the DOM node (just moves it to a new sibling position), which is
// what makes the FLIP measure-before / measure-after work.
const orderedBoards = computed(() => {
  const list = boards.value ?? [];
  const i = list.findIndex((b) => b.alias === activeAlias.value);
  if (i <= 0) return list;
  return [list[i], ...list.slice(0, i), ...list.slice(i + 1)];
});

const root = ref<HTMLElement | null>(null);

// FLIP animation on reorder + focus restoration: if the clicked link was
// moved by the patch (focus drops to <body> on detach), focus lands back
// on it — the clicked board is the active one after navigation.
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
    <!-- One inline run: the separators are real text nodes, so a select-all
         copy of the strip reads "Общий | Игровые системы | ..." on one line.
         The space after a bar is the strip's only wrap opportunity (a board
         and its bar are one nowrap item): the strip wraps, never scrolls. -->
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
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// Inline formatting context, NOT flex: flex blockifies its items, so a
// select-all copy would serialize each item on its own line. With inline
// items the copy reads "Общий | Игровые системы | ..." on one line.
// Left-aligned compact row — spacing comes only from the " | "
// separators, never from justification.
.board-navigation
  display: block
  // 16px down to the leaf content (ForumPage's router-view) — the same
  // strip-to-content distance the leaves had before the strip moved into
  // the persistent shell (flex gap + margin on the board page, collapsed
  // BlockTitle margin on the topic page).
  margin-bottom: $medium
  // FORUM-2: sub-pixel tracking shave so the 11-board strip holds one line
  // a little longer as the content column narrows. No scroll/clip/ellipsis
  // — wrapping at the separator spaces is the narrow-column fallback.
  letter-spacing: -0.01em

.separator
  color: $text-muted
  // 15px, not the 16px base: the forum has 11 boards and the strip must
  // stay on ONE line at typical content widths. The profile Tabs strip
  // (5 short items, tons of room) uses the larger $font-size — full size
  // unification is impossible without wrapping this longer strip.
  font-size: 15px
  // The visible width of the spaces around the bar, and the only knob that
  // widens the strip without touching the copy: it stays "Общий | Игровые
  // системы | ..." with one space on each side (FORUM-1). Two literal spaces
  // in the markup do nothing (they collapse in both the paint and the
  // clipboard) and a flex gap breaks the copy into a column. At +2px the
  // effective space goes from 2.3px to 5.8px, and the 11-board strip still
  // holds one line down to a 1869px window.
  word-spacing: 2px

// A board and the bar after it: the line never breaks between the two.
.board-item
  white-space: nowrap

.board-link
  display: inline-block
  // Multi-word titles never break internally — the strip only wraps at
  // the separator spaces.
  white-space: nowrap
  font-size: 15px
  +link-tone($link)
  // Active item: link color + the site's one bold; underline stays
  // hover-only (underlines are never an active-state indicator on this site).
  &.active
    font-weight: 700
  // Keyboard focus is the text link's ring from Reset.sass: the item is an
  // <a> and a bare word, nothing here to draw differently.
</style>
