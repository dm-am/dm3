<script setup lang="ts" generic="V extends string">
/**
 * Tabs — accessible horizontal tab navigation.
 *
 * Visual: the active tab renders with the same typography as a BlockTitle
 * (uppercase, bold, letter-spacing 0.5px, color `$heading`) — so it reads
 * as a section header. Inactive tabs share the casing and tracking but
 * sit at non-bold weight in `$text-muted`. No underline, no box, no
 * marker glyph — the BlockTitle-equivalent weight + color carry the
 * active state, reinforced by the FLIP shuffle always placing the active
 * tab at position 0.
 *
 * Active-first behavior: the selected tab is always rendered at visual
 * (and DOM) index 0; the rest follow in their declared order. When the
 * selection changes, a manual FLIP pass animates each moved button from
 * its old bounding box to the new one over 280ms.
 *
 * Accessibility: ARIA tablist/tab pattern. Roving `tabindex` keeps only
 * the active tab in the Tab-key cycle. Arrow / Home / End walk the same
 * `orderedTabs` the DOM renders, so keyboard navigation always matches
 * what the user sees.
 *
 * Usage:
 *   <Tabs v-model="active" :tabs="tabs" />
 *   const tabs = [{ value: 'about', label: 'О себе' }, ...]
 */
import { computed, onBeforeUpdate, onUpdated, ref } from "vue";

export interface TabItem<V extends string = string> {
  value: V;
  label: string;
  /** Hide this tab entirely (kept in the list for consistent ordering). */
  hidden?: boolean;
}

const props = withDefaults(
  defineProps<{
    /** Selected tab value (v-model). */
    modelValue: V;
    /** Tab definitions. Order in the array is the declared/canonical order. */
    tabs: readonly TabItem<V>[];
    /** Optional aria-label for the tablist. */
    ariaLabel?: string;
  }>(),
  { ariaLabel: "Разделы" },
);

const emit = defineEmits<{
  "update:modelValue": [value: V];
}>();

// Filter hidden tabs first — both DOM order and visual order operate on
// this set.
const visibleTabs = computed(() => props.tabs.filter((t) => !t.hidden));

// Visual / DOM order: active tab first, rest preserve their declared
// order. Same `:key="tab.value"` for the same item across renders means
// Vue reuses the DOM node (just moves it to a new sibling position),
// which is what makes the FLIP measure-before / measure-after work.
const orderedTabs = computed(() => {
  const tabs = visibleTabs.value;
  const i = tabs.findIndex((t) => t.value === props.modelValue);
  if (i <= 0) return tabs;
  return [tabs[i], ...tabs.slice(0, i), ...tabs.slice(i + 1)];
});

const root = ref<HTMLElement | null>(null);
const buttonRefs = ref<HTMLButtonElement[]>([]);

function setRef(el: Element | object | null, index: number) {
  if (el instanceof HTMLButtonElement) buttonRefs.value[index] = el;
}

function selectTab(value: V) {
  if (value !== props.modelValue) emit("update:modelValue", value);
}

// Keyboard navigation traverses the visual order (= DOM order =
// orderedTabs). ArrowRight from the active tab (always at index 0)
// moves to the first non-active tab in declared order.
function onKeydown(event: KeyboardEvent, currentIndex: number) {
  const tabs = orderedTabs.value;
  const last = tabs.length - 1;
  let nextIndex: number | null = null;

  switch (event.key) {
    case "ArrowRight":
      nextIndex = currentIndex === last ? 0 : currentIndex + 1;
      break;
    case "ArrowLeft":
      nextIndex = currentIndex === 0 ? last : currentIndex - 1;
      break;
    case "Home":
      nextIndex = 0;
      break;
    case "End":
      nextIndex = last;
      break;
    default:
      return;
  }

  event.preventDefault();
  const nextTab = tabs[nextIndex];
  if (nextTab) {
    selectTab(nextTab.value);
    buttonRefs.value[nextIndex]?.focus();
  }
}

// ───────────────────────────── FLIP animation ─────────────────────────────
// Map keyed by DOM element so we can correlate the same button across the
// re-order. `onBeforeUpdate` runs while the DOM is still in the OLD layout;
// we snapshot rects there. `onUpdated` runs after Vue commits the new layout;
// we measure again and animate the delta back to identity.
const FLIP_DURATION_MS = 280;
const FLIP_EASING = "cubic-bezier(0.4, 0, 0.2, 1)";
let prevRects: Map<HTMLButtonElement, DOMRect> | null = null;

// Track in-flight animations so a rapid reorder cancels the stale one
// and starts fresh — no stuck transforms, no animation pile-up.
const activeAnims = new WeakMap<HTMLButtonElement, Animation>();

onBeforeUpdate(() => {
  if (!root.value) return;
  prevRects = new Map();
  for (const btn of root.value.querySelectorAll<HTMLButtonElement>(
    '[role="tab"]',
  )) {
    prevRects.set(btn, btn.getBoundingClientRect());
  }
});

onUpdated(() => {
  if (!prevRects || !root.value) return;
  for (const btn of root.value.querySelectorAll<HTMLButtonElement>(
    '[role="tab"]',
  )) {
    const oldRect = prevRects.get(btn);
    if (!oldRect) continue;
    const newRect = btn.getBoundingClientRect();
    const dx = oldRect.left - newRect.left;
    const dy = oldRect.top - newRect.top;
    if (dx === 0 && dy === 0) continue;

    // Cancel any animation still running on this button from a previous
    // rapid reorder. Otherwise we'd stack interpolations.
    activeAnims.get(btn)?.cancel();

    // Web Animations API: deterministic, decoupled from the CSS transition
    // pipeline (no race with the cascade or with `.tab`'s color/border
    // transitions). The element is at its NEW logical position after
    // Vue's patch; we play it FROM the inverted offset TO identity.
    const anim = btn.animate(
      [
        { transform: `translate(${dx}px, ${dy}px)` },
        { transform: "translate(0, 0)" },
      ],
      {
        duration: FLIP_DURATION_MS,
        easing: FLIP_EASING,
        fill: "none",
      },
    );
    activeAnims.set(btn, anim);
    anim.finished
      .then(() => {
        if (activeAnims.get(btn) === anim) activeAnims.delete(btn);
      })
      .catch(() => {
        /* cancelled — already replaced by a fresh animation */
      });
  }
  prevRects = null;
});
</script>

<template>
  <div ref="root" class="tabs" role="tablist" :aria-label="ariaLabel">
    <!-- Each iteration renders a <button> followed by a real space text
         node. The space is a DOM text-node sibling (NOT inline-content of
         the button, which the renderer would collapse). The Selection API
         captures sibling text nodes, so select-all-copy yields
         "О себе Игры Блоги …" with real spaces between labels. -->
    <template v-for="(tab, index) in orderedTabs" :key="tab.value">
      <button
        :ref="(el) => setRef(el, index)"
        type="button"
        role="tab"
        class="tab"
        :class="{ active: tab.value === modelValue }"
        :aria-selected="tab.value === modelValue"
        :tabindex="tab.value === modelValue ? 0 : -1"
        @click="selectTab(tab.value)"
        @keydown="onKeydown($event, index)"
      >
        {{ tab.label }}</button
      >{{ " " }}
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

.tabs
  display: block
  margin-bottom: $small
  line-height: $control-height

.tab
  display: inline-block
  padding: $small 0
  margin-right: $big
  font-size: $font-size
  font-family: inherit
  font-weight: bold
  letter-spacing: 0.5px
  text-transform: uppercase
  color: $text-muted
  background: none
  border: none
  cursor: pointer
  white-space: nowrap
  // Hard kill any inherited text-decoration so the active tab can never
  // pick up an underline from a global anchor/button rule.
  text-decoration: none
  transition: color $transition-fast

  // Every tab — active or not — uses the same BlockTitle typography
  // (uppercase, bold, letter-spacing 0.5px). The active / inactive /
  // hover states differ ONLY by color, which keeps the strip's geometry
  // stable through clicks and through the FLIP shuffle.
  &:hover:not(.active)
    color: $text

  // Active tab matches BlockTitle (the "Контактная информация" h2 above the
  // strip) — same weight + tracking + casing AND `$heading` color, so it
  // reads as a section header for the panel below it.
  &.active
    color: $heading

  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px
</style>
