<script setup lang="ts">
/**
 * The switch of the editor's two modes, "[bbcode]" and "wysiwyg", with the
 * line under the active one. It asks the editor for the mode and does not set
 * it: the editor carries the text across when the mode changes.
 */
import { nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import type { EditorMode } from "./lib/sourceField";

const props = defineProps<{ modelValue: EditorMode }>();

const emit = defineEmits<{
  (e: "update:modelValue", mode: EditorMode): void;
}>();

const modeTabsRef = ref<HTMLElement | null>(null);

// Indicator position for smooth animation
const indicatorStyle = ref<Record<string, string>>({
  left: "0px",
  width: "50%",
});

/**
 * Lays the line under the active tab. It slides there when the mode changes,
 * and is put there at once when a tab changes size under it: the tabs are
 * first measured before the site's font has arrived, and "wysiwyg" in the
 * fallback font was 3px wider, so the line stood past the editor's right edge
 * until the mode was switched. The box is read in fractions of a pixel, since
 * offsetLeft and offsetWidth round and left the line a third of one past the tab.
 */
function updateIndicator(slide = true) {
  if (!modeTabsRef.value) return;
  const tabs = modeTabsRef.value.querySelectorAll<HTMLElement>(".mode-tab");
  const activeTab = tabs[props.modelValue === "bbcode" ? 0 : 1];
  if (!activeTab) return;
  const strip = modeTabsRef.value.getBoundingClientRect();
  const tab = activeTab.getBoundingClientRect();
  indicatorStyle.value = {
    left: `${tab.left - strip.left}px`,
    width: `${tab.width}px`,
    ...(slide ? {} : { transition: "none" }),
  };
}

watch(
  () => props.modelValue,
  () => {
    nextTick(() => updateIndicator());
  },
);

// Watches the two tabs for a change of size, the arrival of the font among them.
let modeTabsObserver: ResizeObserver | null = null;

function observeModeTabs() {
  if (!modeTabsRef.value || typeof ResizeObserver === "undefined") return;
  modeTabsObserver = new ResizeObserver(() => updateIndicator(false));
  for (const tab of modeTabsRef.value.querySelectorAll(".mode-tab")) {
    modeTabsObserver.observe(tab);
  }
}

onMounted(() => {
  // Initialize mode indicator position
  nextTick(() => updateIndicator());
  observeModeTabs();
});

onUnmounted(() => {
  modeTabsObserver?.disconnect();
});
</script>

<template>
  <div ref="modeTabsRef" class="mode-tabs">
    <Tooltip text="Режим BBCode">
      <button
        type="button"
        class="mode-tab"
        :class="{ active: modelValue === 'bbcode' }"
        @click="emit('update:modelValue', 'bbcode')"
        aria-label="Режим BBCode"
      >
        <small>[bbcode]</small>
      </button>
    </Tooltip>
    <!-- Zero-width preserved space: the pair copies as "[bbcode] wysiwyg"
         and gains no gap on screen (the global .copy-space, Reset.sass). -->
    <span class="copy-space">{{ " " }}</span>
    <Tooltip text="Визуальный редактор">
      <button
        type="button"
        class="mode-tab"
        :class="{ active: modelValue === 'wysiwyg' }"
        @click="emit('update:modelValue', 'wysiwyg')"
        aria-label="Визуальный редактор"
      >
        <small><i>wysiwyg</i></small>
      </button>
    </Tooltip>
    <span class="mode-indicator" :style="indicatorStyle"></span>
  </div>
</template>

<style scoped lang="sass">
// Inline flow, not flex, for the same reason as the status bar: flex items
// copy one per line, and this pair copied as "[bbcode]\nwysiwyg". font-size: 0
// removes the container's own line-box strut, so the tabs keep exactly the
// height the flex row gave them (each button restates its own font size).
.mode-tabs
  display: block
  flex-shrink: 0
  white-space: nowrap
  font-size: 0
  border-bottom: 2px solid $border
  position: relative

.mode-tab
  display: inline-block
  vertical-align: top
  padding: $tiny $small
  border: none
  background: none
  color: $text-muted
  font-family: inherit
  font-size: $secondary-font-size
  cursor: pointer
  transition: filter $transition-normal

  &:hover:not(.active)
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)
    cursor: default

.mode-indicator
  position: absolute
  bottom: -2px
  height: 2px
  background-color: $text-muted
  transition: left $transition-slow, width $transition-slow
  pointer-events: none
</style>
