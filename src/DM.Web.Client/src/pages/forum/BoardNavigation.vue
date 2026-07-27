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
</script>

<template>
  <nav
    v-if="boards?.length"
    ref="root"
    class="board-navigation"
    aria-label="Разделы форума"
  >
    <!-- The separator is a real " | " text node, so a select-all copy of
         the strip reads "Общий | Игровые системы | ..." with actual
         spaces. Those spaces are also the strip's only wrap opportunities
         (items themselves are nowrap): the strip wraps, never scrolls. -->
    <template v-for="(board, idx) in orderedBoards" :key="board.id">
      <span v-if="idx > 0" class="separator" aria-hidden="true">{{
        " | "
      }}</span>
      <router-link
        :to="{ name: 'forum', params: { alias: board.alias } }"
        :class="['board-link', { active: activeAlias === board.alias }]"
        :aria-current="activeAlias === board.alias ? 'page' : undefined"
        >{{ board.title }}</router-link
      >
    </template>
  </nav>
</template>

<style scoped lang="sass">
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
  // — wrapping at the separator spaces stays the narrow-column fallback.
  letter-spacing: -0.01em

.separator
  color: $text-muted
  // 15px, not the 16px base: the forum has 11 boards and the strip must
  // stay on ONE line at typical content widths. The profile Tabs strip
  // (5 short items, tons of room) uses the larger $font-size — full size
  // unification is impossible without wrapping this longer strip.
  font-size: 15px
  // FORUM-2: visually tightens the literal " | " spaces; the copy still
  // reads "Общий | Игровые системы | ..." with real spaces (FORUM-1).
  word-spacing: -1.5px

.board-link
  display: inline-block
  color: $link
  text-decoration: none
  // Multi-word titles never break internally — the strip only wraps at
  // the separator spaces.
  white-space: nowrap
  font-size: 15px
  &:hover
    color: $link-hover
    text-decoration: underline
  // Active item: link color + semibold; underline stays hover-only
  // (underlines are never an active-state indicator on this site).
  &.active
    font-weight: 600
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px
</style>
