<script setup lang="ts" generic="V extends string">
// The selected tab renders first, and a FLIP animates the move. The keys walk the declared order, and
// aria-posinset publishes it: by rendered position they bounce between two tabs. The caller wires its
// tabpanel with the exposed tabId and panelId.
import { computed, nextTick, ref, useId } from "vue";
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
    /** "headings": the strip doubles as the section heading of the profile, in PageTitle's style. */
    variant?: "links" | "headings";
  }>(),
  {
    ariaLabel: "Разделы",
    variant: "links",
  },
);

/** Builds the tab and panel ids: an instance id, so two strips never collide. */
const idPrefix = `tabs-${useId()}`;

/** For the caller's tabpanel aria-labelledby. */
function tabId(value: V): string {
  return `${idPrefix}-tab-${value}`;
}

/** For the caller's tabpanel id. */
function panelId(value: V): string {
  return `${idPrefix}-panel-${value}`;
}

defineExpose({ tabId, panelId });

const emit = defineEmits<{
  "update:modelValue": [value: V];
}>();

const visibleTabs = computed(() => props.tabs.filter((t) => !t.hidden));

// Active first; the same key reuses the DOM node, which is what the FLIP measures.
const orderedTabs = computed(() => {
  const tabs = visibleTabs.value;
  const i = tabs.findIndex((t) => t.value === props.modelValue);
  if (i <= 0) return tabs;
  return [tabs[i], ...tabs.slice(0, i), ...tabs.slice(i + 1)];
});

// Without it every selected tab reads as "1 of N".
const declaredPosition = computed(
  () =>
    new Map<V, number>(
      visibleTabs.value.map((t, i): [V, number] => [t.value, i + 1]),
    ),
);

const root = ref<HTMLElement | null>(null);

/** Whether a tab has another after it, and so a bar. */
const hasNext = (index: number) => index < orderedTabs.value.length - 1;

// The roving-tabindex target, wherever the reorder put it.
const SELECTED_TAB_SELECTOR = '[role="tab"][tabindex="0"]';

function selectTab(value: V) {
  if (value !== props.modelValue) emit("update:modelValue", value);
}

// The declared order: the selected tab re-renders at index 0, so rendered positions bounce.
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
  // Focus follows the selection once the strip has re-rendered.
  nextTick(() => {
    root.value
      ?.querySelector<HTMLElement>(SELECTED_TAB_SELECTOR)
      ?.focus({ preventScroll: true });
  });
}

// Vue's keyed diff detaches the focused button, and the browser drops focus to <body>.
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
    <!-- One inline run: the bars are real text, and the space after a bar is the strip's only wrap. -->
    <template v-for="(tab, index) in orderedTabs" :key="tab.value"
      ><span class="tabs__item"
        ><button
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
          {{ tab.label }}</button
        ><span v-if="hasNext(index)" class="separator" aria-hidden="true">{{
          " |"
        }}</span></span
      ><span v-if="hasNext(index)" class="separator" aria-hidden="true">{{
        " "
      }}</span></template
    >
  </div>
</template>

<style scoped lang="sass">
// Inline, not flex: flex items copy as separate lines.
.tabs
  display: block
  margin-bottom: $small

// A tab and the bar after it: the line never breaks between the two.
.tabs__item
  white-space: nowrap

.separator
  font-size: $font-size
  color: $text-muted

.tab
  // inline-block: the label stays whole and the FLIP transform applies.
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
  // No underline from a global rule outside :hover.
  text-decoration: none
  // The tempo of every link of the site (Reset.sass, +link-tone).
  transition: color $transition-normal

  &:hover
    color: $link-hover
    text-decoration: underline

  // Bold, as the board strip's current item: the face has two weights.
  &.active
    font-weight: 700

  // A word, not a box: the thin ring of a text link.
  &:focus-visible
    outline: 1px solid $link
    outline-offset: 2px

// The active tab is PageTitle exactly; the rest the same, greyed.
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
