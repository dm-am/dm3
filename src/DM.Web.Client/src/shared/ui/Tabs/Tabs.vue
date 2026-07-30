<script setup lang="ts" generic="V extends string">
/**
 * Tabs — accessible horizontal tab navigation.
 *
 * Visual: unified with the forum board strip (pages/forum/
 * BoardNavigation.vue) — the two must look identical: a compact
 * left-aligned row of items with " | " separators in base ($font-size) typography.
 * Items behave like the site's regular links: `$link` color,
 * `$link-hover` + underline on hover; the active tab keeps the link
 * color and is marked by semibold weight (underline only on hover —
 * underlines are never an active-state indicator on this site).
 *
 * Active-first behavior: the selected tab is always rendered at visual
 * (and DOM) index 0; the rest follow in their declared order. When the
 * selection changes, a manual FLIP pass animates each moved button from
 * its old bounding box to the new one over 280ms. Only the buttons are
 * animated: the " | " separators are visually identical glyphs, so they
 * simply re-render at their new slots without drawing attention (a
 * separator cannot be transform-animated anyway without breaking the
 * strip's wrap-at-separator behavior).
 *
 * Accessibility: ARIA tablist/tab pattern with automatic activation.
 * Roving `tabindex` keeps only the active tab in the Tab-key cycle.
 * Arrow / Home / End walk the DECLARED order (the `tabs` prop), never the
 * rendered active-first one: the selection always re-renders at index 0,
 * so stepping by rendered position ping-pongs between two tabs and never
 * reaches the third. `aria-posinset` / `aria-setsize` publish that same
 * declared order, so the position assistive tech announces is the one the
 * arrows walk. Each tab button gets a stable `id`
 * and `aria-controls`; use the exposed `tabId`/`panelId` helpers (via a
 * template ref) to wire the matching `role="tabpanel"` element on the
 * caller's side: `id="panelId(active)"` + `aria-labelledby="tabId(active)"`.
 *
 * Usage:
 *   <Tabs ref="tabsRef" v-model="active" :tabs="tabs" />
 *   const tabs = [{ value: 'about', label: 'О себе' }, ...]
 */
import { computed, nextTick, ref } from "vue";
import { useFlipReorder } from "@/shared/lib/composables/useFlipReorder";

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
    /**
     * Prefix used to build per-tab / per-panel DOM ids: `${idPrefix}-tab-${value}`
     * and `${idPrefix}-panel-${value}`. Defaults to a random instance id so
     * multiple <Tabs> on the same page never collide.
     */
    idPrefix?: string;
    /**
     * Visual variant. "links" (default): a compact row of link-styled items
     * with " | " separators — the forum board-strip idiom. "headings": the
     * strip reads as a row of page-title-sized section headings — the active
     * tab matches <PageTitle> (brown $heading, uppercase, bold), the others
     * the same but greyed. Used by the profile page, where each tab is a
     * section of the profile and the strip doubles as its section heading.
     */
    variant?: "links" | "headings";
  }>(),
  {
    ariaLabel: "Разделы",
    idPrefix: () => `tabs-${Math.random().toString(36).slice(2, 9)}`,
    variant: "links",
  },
);

/** Tab-button DOM id for a given tab value — also usable by the caller
 * (via `tabId`) to wire `aria-labelledby` on its own tabpanel element. */
function tabId(value: V): string {
  return `${props.idPrefix}-tab-${value}`;
}

/** Tabpanel DOM id for a given tab value — the caller sets this as the
 * `id` on its own tabpanel element and points `aria-controls` here. */
function panelId(value: V): string {
  return `${props.idPrefix}-panel-${value}`;
}

defineExpose({ tabId, panelId });

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

// 1-based position in the DECLARED order, published as `aria-posinset`.
// The rendered order is active-first, so without it assistive tech reads
// every selected tab as "1 of N" and the announced sequence contradicts
// the one the arrow keys walk.
const declaredPosition = computed(
  () =>
    new Map<V, number>(
      visibleTabs.value.map((t, i): [V, number] => [t.value, i + 1]),
    ),
);

const root = ref<HTMLElement | null>(null);

// The roving-tabindex target is by definition the selected tab, wherever
// the active-first reorder has just put it. One selector serves both the
// post-selection focus move and useFlipReorder's focus restoration.
const SELECTED_TAB_SELECTOR = '[role="tab"][tabindex="0"]';

function selectTab(value: V) {
  if (value !== props.modelValue) emit("update:modelValue", value);
}

// Keyboard navigation steps through the DECLARED order (visibleTabs) and
// lets the selection carry the focus with it. Stepping through rendered
// positions instead is a dead end: the selected tab always re-renders at
// index 0, so ArrowRight/ArrowLeft would bounce between two tabs forever
// and Home, aimed at rendered index 0, would reselect the current tab.
function onKeydown(event: KeyboardEvent) {
  const tabs = visibleTabs.value;
  const last = tabs.length - 1;
  if (last < 0) return;
  const current = tabs.findIndex((t) => t.value === props.modelValue);
  let nextIndex: number;

  switch (event.key) {
    case "ArrowRight":
      nextIndex = current >= last ? 0 : current + 1;
      break;
    case "ArrowLeft":
      nextIndex = current <= 0 ? last : current - 1;
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
  selectTab(tabs[nextIndex].value);
  // Focus follows the selection: once the strip has re-rendered, the
  // chosen tab is the only one left in the Tab cycle.
  nextTick(() => {
    root.value
      ?.querySelector<HTMLElement>(SELECTED_TAB_SELECTOR)
      ?.focus({ preventScroll: true });
  });
}

// FLIP animation on reorder + focus restoration (the browser drops focus
// to <body> when Vue's keyed diff detaches the focused button — restore it
// onto the active tab, the roving tabindex target). Shared mechanics live
// in useFlipReorder (also used by the forum BoardNavigation strip).
useFlipReorder({
  root,
  itemSelector: '[role="tab"]',
  focusSelector: SELECTED_TAB_SELECTOR,
});
</script>

<template>
  <div
    ref="root"
    class="tabs"
    :class="'tabs--' + variant"
    role="tablist"
    :aria-label="ariaLabel"
  >
    <!-- The separator is a real " | " text node, so a select-all copy of
         the strip reads "О себе | Игры | ..." with actual spaces. Those
         spaces are also the strip's only wrap opportunities (buttons are
         nowrap). aria-hidden keeps the decorative separators out of the
         tablist's accessible content. -->
    <template v-for="(tab, index) in orderedTabs" :key="tab.value">
      <span v-if="index > 0" class="separator" aria-hidden="true">{{
        " | "
      }}</span>
      <button
        :id="tabId(tab.value)"
        type="button"
        role="tab"
        class="tab"
        :class="{ active: tab.value === modelValue }"
        :aria-selected="tab.value === modelValue"
        :aria-controls="panelId(tab.value)"
        :aria-posinset="declaredPosition.get(tab.value)"
        :aria-setsize="visibleTabs.length"
        :tabindex="tab.value === modelValue ? 0 : -1"
        @click="selectTab(tab.value)"
        @keydown="onKeydown"
      >
        {{ tab.label }}
      </button>
    </template>
  </div>
</template>

<style scoped lang="sass">
// Inline formatting context, NOT flex: flex blockifies its items, so a
// select-all copy would serialize each item on its own line. With inline
// items the copy reads "О себе | Игры | ..." on one line. Left-aligned
// compact row — spacing comes only from the " | " separators, never
// from justification.
.tabs
  display: block
  margin-bottom: $small

.separator
  color: $text-muted
  font-size: $font-size

.tab
  // inline-block: keeps the label atomic (no internal wrapping) and
  // makes the FLIP transform applicable.
  display: inline-block
  padding: 0
  font-size: $font-size
  font-family: inherit
  font-weight: normal
  color: $link
  background: none
  border: none
  cursor: pointer
  white-space: nowrap
  // Hard kill any inherited text-decoration so a tab can never pick up
  // an underline from a global anchor/button rule outside :hover.
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

  // Active tab: link color + semibold — same idiom as the forum board
  // strip's current item; underline stays hover-only.
  &.active
    font-weight: 600

  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

// "headings" variant: the strip reads as a row of page-title-sized section
// headings (the profile page — each tab is a section of the profile, and the
// strip doubles as the section heading). The active tab matches the page
// <PageTitle> exactly (brown $heading, $title-font-size, bold, uppercase,
// 0.5px tracking); the rest keep the same format greyed ($heading-alt), so
// the strip reads as the current section's heading among its muted siblings.
.tabs--headings
  .separator
    font-size: $title-font-size
    color: $heading-alt

  .tab
    font-size: $title-font-size
    font-weight: bold
    text-transform: uppercase
    letter-spacing: 0.5px
    color: $heading-alt

    &:hover
      color: $heading
      text-decoration: none

    &.active
      font-weight: bold
      color: $heading
</style>
